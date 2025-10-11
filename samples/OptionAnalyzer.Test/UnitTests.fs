module OptionAnalyzer.Test

open FSharp.Compiler.CodeAnalysis
open NUnit.Framework
open FSharp.Compiler.Text
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.Testing

let mutable projectOptions: FSharpProjectOptions = FSharpProjectOptions.zero

[<SetUp>]
let Setup () =
    task {
        let! opts =
            mkOptionsFromProject
                "net8.0"
                [
                    {
                        Name = "Newtonsoft.Json"
                        Version = "13.0.3"
                    }
                    {
                        Name = "Fantomas.FCS"
                        Version = "6.2.0"
                    }
                ]

        projectOptions <- opts
    }

[<Test>]
let ``warnings are emitted`` () =
    async {
        let source =
            """
module M

let notUsed() =
    let option : Option<int> = None
    option.Value
    """

        let ctx = getContext projectOptions source
        let! msgs = optionValueAnalyzer ctx
        Assert.IsNotEmpty msgs
        Assert.IsTrue(Assert.messageContains "Option.Value" msgs[0])

    }

[<Test>]
let ``expected warning is emitted`` () =
    async {
        let source =
            """
module M

open Newtonsoft.Json
open Fantomas.FCS

let json = JsonConvert.SerializeObject([1;2;3])

let p = Fantomas.FCS.Text.Position.mkPos 23 2

let notUsed() =
    let option : Option<int> = None
    option.Value
    """

        let expectedMsg =
            {
                Code = "OV001"
                Fixes = []
                Message = "Option.Value shouldn't be used"
                Range = Range.mkRange "A.fs" (Position.mkPos 13 4) (Position.mkPos 13 16)
                Severity = Severity.Warning
                Type = "Option.Value analyzer"
            }

        let ctx = getContext projectOptions source
        let! msgs = optionValueAnalyzer ctx
        Assert.IsTrue(msgs |> List.contains expectedMsg)
    }

let tryCompareRanges code expected (results: Map<string, AnalyzerIgnoreRange list>) =
    match Map.tryFind code results with
    | None -> Assert.Fail(sprintf "Expected to find %s in result" code)
    | Some ranges ->
        Assert.That(ranges, Is.EquivalentTo(expected))

[<Test>]
let ``get single line scoped ignore with one code`` () =
    async {
        let source = """
module M
// IGNORE: IONIDE-001
let x = 1
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [SingleLine 3]
    }

[<Test>]
let ``get single line scoped ignore with multiple codes`` () =
    async {
        let source = """
module M
// IGNORE: IONIDE-001, IONIDE-002
let x = 1
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [SingleLine 3]
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-002" [SingleLine 3]
    }

[<Test>]
let ``get file scoped ignore`` () =
    async {
        let source = """
module M
// IGNORE FILE: IONIDE-001
let x = 1
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [File]
    }

[<Test>]
let ``get file scoped ignore with multiple codes`` () =
    async {
        let source = """
module M
// IGNORE FILE: IONIDE-001, IONIDE-002, IONIDE-003
let x = 1
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [File]
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-002" [File]
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-003" [File]
    }

[<Test>]
let ``get range scoped ignore`` () =
    async {
        let source = """
module M
// IGNORE START: IONIDE-001
let x = 1
// IGNORE END
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [Range (3, 5)]
    }

[<Test>]
let ``get range scoped ignore with multiple codes`` () =
    async {
        let source = """
module M
// IGNORE START: IONIDE-001, IONIDE-002
let x = 1
// IGNORE END
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [Range (3, 5)]
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-002" [Range (3, 5)]
    }

[<Test>]
let ``get range scoped ignore handles nested ignores`` () =
    async {
        let source = """
module M
// IGNORE START: IONIDE-001
// IGNORE START: IONIDE-002
let x = 1
// IGNORE END
// IGNORE END
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [Range (3, 7)]
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-002" [Range (4, 6)]
    }

[<Test>]
let ``ignores unclosed range scoped ignore`` () =
    async {
        let source = """
module M
// IGNORE START: IONIDE-001
let x = 1
"""
        let ctx = getContext projectOptions source
        Assert.That(ctx.AnalyzerIgnoreRanges, Is.Empty)
    }

[<Test>]
let ``ignores unopened range scoped ignore`` () =
    async {
        let source = """
module M
let x = 1
// IGNORE END
"""
        let ctx = getContext projectOptions source
        Assert.That(ctx.AnalyzerIgnoreRanges, Is.Empty)
    }

[<Test>]
let ``code can have multiple ranges for one code`` () =
    async {
        let source = """
// IGNORE FILE: IONIDE-001
module M
// IGNORE START: IONIDE-001
// IGNORE: IONIDE-001
let x = 1
// IGNORE END
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges 
        |> tryCompareRanges "IONIDE-001" [
            File
            SingleLine 5
            Range (4, 7)
        ]
    }

[<Test>]
let ``single line ignore handles tight spacing`` () =
    async {
        let source = """
module M
// IGNORE:IONIDE-001
let x = 1
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [SingleLine 3]
    }

[<Test>]
let ``single line ignore handles loose spacing`` () =
    async {
        let source = """
module M
// IGNORE    :     IONIDE-001
let x = 1
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [SingleLine 3]
    }

[<Test>]
let ``single line, multi-code ignore handles tight spacing`` () =
    async {
        let source = """
module M
// IGNORE:IONIDE-001,IONIDE-002
let x = 1
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [SingleLine 3]
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-002" [SingleLine 3]
    }

[<Test>]
let ``single line, multi-code ignore handles loose spacing`` () =
    async {
        let source = """
module M
// IGNORE    :     IONIDE-001   ,    IONIDE-002
let x = 1
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [SingleLine 3]
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-002" [SingleLine 3]
    }

[<Test>]
let ``file ignore handles tight spacing`` () =
    async {
        let source = """
module M
// IGNORE FILE:IONIDE-001
let x = 1
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [File]
    }

[<Test>]
let ``file ignore handles loose spacing`` () =
    async {
        let source = """
module M
// IGNORE FILE :     IONIDE-001
let x = 1
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [File]
    }



[<Test>]
let ``range ignore handles tight spacing`` () =
    async {
        let source = """
module M
// IGNORE START:IONIDE-001
let x = 1
// IGNORE END
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [Range (3, 5)]
    }

[<Test>]
let ``range ignore handles loose spacing`` () =
    async {
        let source = """
module M
// IGNORE START   :     IONIDE-001
let x = 1
// IGNORE END
"""
        let ctx = getContext projectOptions source
        ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [Range (3, 5)]
    }