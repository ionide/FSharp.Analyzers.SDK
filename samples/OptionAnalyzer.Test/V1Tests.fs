module OptionAnalyzer.V1Tests

#nowarn "57"

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open NUnit.Framework
open FsCheck.NUnit
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.Testing
open FSharp.Analyzers.SDK.AdapterV1

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

// ─── Oracle tests: V1 and legacy analyzers must agree ──────────────

module OracleTests =

    [<Test>]
    let ``V1 and legacy agree on single Option.Value`` () =
        async {
            let source =
                """
module M

let f () =
    let option : Option<int> = None
    option.Value
    """

            let ctx = getContext projectOptions source
            let! legacyMsgs = OptionAnalyzer.optionValueAnalyzer ctx

            let v1Ctx = contextToV1 ctx
            let! v1Raw = OptionAnalyzer.V1.optionValueAnalyzer v1Ctx

            let v1Msgs =
                v1Raw
                |> List.map messageFromV1

            Assert.AreEqual(legacyMsgs.Length, v1Msgs.Length, "message count")

            for legacy, v1 in List.zip legacyMsgs v1Msgs do
                Assert.AreEqual(legacy, v1)
        }

    [<Test>]
    let ``V1 and legacy agree on multiple Option.Value usages`` () =
        async {
            let source =
                """
module M

let f () =
    let a : Option<int> = None
    let b : Option<string> = None
    let _ = a.Value
    let _ = b.Value
    a.Value + b.Value.Length
    """

            let ctx = getContext projectOptions source
            let! legacyMsgs = OptionAnalyzer.optionValueAnalyzer ctx

            let v1Ctx = contextToV1 ctx
            let! v1Raw = OptionAnalyzer.V1.optionValueAnalyzer v1Ctx

            let v1Msgs =
                v1Raw
                |> List.map messageFromV1

            let sort msgs =
                msgs
                |> List.sortBy (fun (m: Message) -> m.Range.StartLine, m.Range.StartColumn)

            Assert.AreEqual(legacyMsgs.Length, v1Msgs.Length, "message count")

            for legacy, v1 in List.zip (sort legacyMsgs) (sort v1Msgs) do
                Assert.AreEqual(legacy, v1)
        }

    [<Test>]
    let ``V1 and legacy agree on clean input`` () =
        async {
            let source =
                """
module M

let f () =
    let x = Some 42
    match x with
    | Some v -> v
    | None -> 0
    """

            let ctx = getContext projectOptions source
            let! legacyMsgs = OptionAnalyzer.optionValueAnalyzer ctx

            let v1Ctx = contextToV1 ctx
            let! v1Raw = OptionAnalyzer.V1.optionValueAnalyzer v1Ctx

            let v1Msgs =
                v1Raw
                |> List.map messageFromV1

            Assert.IsEmpty legacyMsgs
            Assert.IsEmpty v1Msgs
        }

// ─── Client integration tests ──────────────────────────────────────

module ClientIntegrationTests =

    [<Test>]
    let ``LoadAnalyzers includes V1 analyzers in count`` () =
        let client = Client<CliAnalyzerAttribute, CliContext>()
        let path = System.IO.Path.GetFullPath(".")
        let stats = client.LoadAnalyzers(path)
        Assert.That(stats.Analyzers, Is.GreaterThanOrEqualTo 2)

    [<Test>]
    let ``RunAnalyzersSafely produces results from both legacy and V1`` () =
        async {
            let source =
                """
module M

let notUsed() =
    let option : Option<int> = None
    option.Value
    """

            let ctx = getContext projectOptions source
            let client = Client<CliAnalyzerAttribute, CliContext>()
            let path = System.IO.Path.GetFullPath(".")
            let _stats = client.LoadAnalyzers(path)
            let! results = client.RunAnalyzersSafely(ctx)

            let optionResults =
                results
                |> List.filter (fun r -> r.AnalyzerName = "OptionAnalyzer")

            Assert.That(
                optionResults.Length,
                Is.GreaterThanOrEqualTo 2,
                "Expected results from both legacy and V1 OptionAnalyzer"
            )

            for r in optionResults do
                match r.Output with
                | Ok msgs ->
                    Assert.That(msgs, Is.Not.Empty)

                    for msg in msgs do
                        Assert.AreEqual("OV001", msg.Code)
                        Assert.AreEqual(Severity.Warning, msg.Severity)
                | Error ex -> Assert.Fail($"Analyzer result was Error: %A{ex}")
        }

// ─── Adapter unit tests ────────────────────────────────────────────

module AdapterTests =

    // Abbreviations for V1 types to avoid ambiguity with SDK types.
    type V1Severity = FSharp.Analyzers.SDK.V1.Severity
    type V1SourceRange = FSharp.Analyzers.SDK.V1.SourceRange
    type V1Fix = FSharp.Analyzers.SDK.V1.Fix
    type V1Message = FSharp.Analyzers.SDK.V1.Message
    type V1AnalyzerIgnoreRange = FSharp.Analyzers.SDK.V1.AnalyzerIgnoreRange

    [<Property>]
    let ``rangeToV1 then rangeFromV1 preserves all fields``
        (sl: int)
        (sc: int)
        (ls: int)
        (ec: int)
        =
        // Constrain to valid Position.mkPos inputs (line >= 1, col >= 0).
        // Mask sign bit to avoid abs(Int32.MinValue) overflow.
        let startLine =
            (sl
             &&& 0x7FFFFFFF) % 10000
            + 1

        let startCol =
            (sc
             &&& 0x7FFFFFFF) % 200

        let endLine =
            startLine
            + (ls
               &&& 0x7FFFFFFF) % 100

        let endCol =
            (ec
             &&& 0x7FFFFFFF) % 200

        let r =
            Range.mkRange
                "Test.fs"
                (Position.mkPos startLine startCol)
                (Position.mkPos endLine endCol)

        let rt = rangeFromV1 (rangeToV1 r)

        rt.FileName = r.FileName
        && rt.StartLine = r.StartLine
        && rt.StartColumn = r.StartColumn
        && rt.EndLine = r.EndLine
        && rt.EndColumn = r.EndColumn

    [<Test>]
    let ``severityFromV1 maps all cases correctly`` () =
        Assert.AreEqual(Severity.Info, severityFromV1 V1Severity.Info)
        Assert.AreEqual(Severity.Hint, severityFromV1 V1Severity.Hint)
        Assert.AreEqual(Severity.Warning, severityFromV1 V1Severity.Warning)
        Assert.AreEqual(Severity.Error, severityFromV1 V1Severity.Error)

    [<Test>]
    let ``messageFromV1 preserves all fields`` () =
        let v1Range: V1SourceRange =
            {
                FileName = "Test.fs"
                StartLine = 10
                StartColumn = 4
                EndLine = 10
                EndColumn = 16
            }

        let v1Fix: V1Fix =
            {
                FromRange = v1Range
                FromText = "old"
                ToText = "new"
            }

        let v1Msg: V1Message =
            {
                Type = "TestType"
                Message = "TestMessage"
                Code = "T001"
                Severity = V1Severity.Error
                Range = v1Range
                Fixes = [ v1Fix ]
            }

        let sdkMsg = messageFromV1 v1Msg

        Assert.AreEqual("TestType", sdkMsg.Type)
        Assert.AreEqual("TestMessage", sdkMsg.Message)
        Assert.AreEqual("T001", sdkMsg.Code)
        Assert.AreEqual(Severity.Error, sdkMsg.Severity)
        Assert.AreEqual("Test.fs", sdkMsg.Range.FileName)
        Assert.AreEqual(10, sdkMsg.Range.StartLine)
        Assert.AreEqual(4, sdkMsg.Range.StartColumn)
        Assert.AreEqual(10, sdkMsg.Range.EndLine)
        Assert.AreEqual(16, sdkMsg.Range.EndColumn)
        Assert.AreEqual(1, sdkMsg.Fixes.Length)
        Assert.AreEqual("old", sdkMsg.Fixes[0].FromText)
        Assert.AreEqual("new", sdkMsg.Fixes[0].ToText)
        Assert.AreEqual(10, sdkMsg.Fixes[0].FromRange.StartLine)

    [<Test>]
    let ``analyzerIgnoreRangeToV1 maps all cases`` () =
        Assert.AreEqual(
            V1AnalyzerIgnoreRange.File,
            analyzerIgnoreRangeToV1 AnalyzerIgnoreRange.File
        )

        Assert.AreEqual(
            V1AnalyzerIgnoreRange.Range(3, 7),
            analyzerIgnoreRangeToV1 (AnalyzerIgnoreRange.Range(3, 7))
        )

        Assert.AreEqual(
            V1AnalyzerIgnoreRange.NextLine 5,
            analyzerIgnoreRangeToV1 (AnalyzerIgnoreRange.NextLine 5)
        )

        Assert.AreEqual(
            V1AnalyzerIgnoreRange.CurrentLine 10,
            analyzerIgnoreRangeToV1 (AnalyzerIgnoreRange.CurrentLine 10)
        )

    [<Test>]
    let ``contextToV1 preserves filename and source text`` () =
        let source =
            """
module M
let x = 1
"""

        let ctx = getContext projectOptions source
        let v1Ctx = contextToV1 ctx

        Assert.AreEqual(ctx.FileName, v1Ctx.FileName)

        let expectedText = ctx.SourceText.GetSubTextString(0, ctx.SourceText.Length)
        Assert.AreEqual(expectedText, v1Ctx.SourceText)

    [<Test>]
    let ``contextToV1 preserves project options`` () =
        let source =
            """
module M
let x = 1
"""

        let ctx = getContext projectOptions source
        let v1Ctx = contextToV1 ctx

        Assert.AreEqual(ctx.ProjectOptions.ProjectFileName, v1Ctx.ProjectOptions.ProjectFileName)

        Assert.AreEqual(ctx.ProjectOptions.SourceFiles, v1Ctx.ProjectOptions.SourceFiles)

        Assert.AreEqual(ctx.ProjectOptions.OtherOptions, v1Ctx.ProjectOptions.OtherOptions)
