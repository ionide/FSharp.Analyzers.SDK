module OptionAnalyzer.Test

open FSharp.Compiler.CodeAnalysis
open NUnit.Framework
open FSharp.Compiler.Text
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.Testing
open FSharp.Analyzers.SDK

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

module IgnoreRangeTests =

    let tryCompareRanges code expected (results: Map<string, AnalyzerIgnoreRange list>) =
        match Map.tryFind code results with
        | None -> Assert.Fail(sprintf "Expected to find %s in result" code)
        | Some ranges -> Assert.That(ranges, Is.EquivalentTo(expected))

    [<Test>]
    let ``get next line scoped ignore with one code`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer: ignore-line-next IONIDE-001
    let x = 1
    """

            let ctx = getContext projectOptions source
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [ NextLine 3 ]
        }

    [<Test>]
    let ``get next line scoped ignore with multiple codes`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer: ignore-line-next IONIDE-001, IONIDE-002
    let x = 1
    """

            let ctx = getContext projectOptions source
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [ NextLine 3 ]
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-002" [ NextLine 3 ]
        }

    [<Test>]
    let ``get current line scoped ignore with one code`` () =
        async {
            let source =
                """
    module M
    let x = 1 // fsharpanalyzer: ignore-line IONIDE-001
    """

            let ctx = getContext projectOptions source
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [ CurrentLine 3 ]
        }

    [<Test>]
    let ``get current line scoped ignore with multiple codes`` () =
        async {
            let source =
                """
    module M
    let x = 1 // fsharpanalyzer: ignore-line IONIDE-001, IONIDE-002
    """

            let ctx = getContext projectOptions source
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [ CurrentLine 3 ]
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-002" [ CurrentLine 3 ]
        }

    [<Test>]
    let ``get file scoped ignore`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer: ignore-file IONIDE-001
    let x = 1
    """

            let ctx = getContext projectOptions source
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [ File ]
        }

    [<Test>]
    let ``get file scoped ignore with multiple codes`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer: ignore-file IONIDE-001, IONIDE-002, IONIDE-003
    let x = 1
    """

            let ctx = getContext projectOptions source
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [ File ]
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-002" [ File ]
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-003" [ File ]
        }

    [<Test>]
    let ``get range scoped ignore`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer: ignore-region-start IONIDE-001
    let x = 1
    // fsharpanalyzer: ignore-region-end
    """

            let ctx = getContext projectOptions source
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [ Range(3, 5) ]
        }

    [<Test>]
    let ``get range scoped ignore with multiple codes`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer: ignore-region-start IONIDE-001, IONIDE-002
    let x = 1
    // fsharpanalyzer: ignore-region-end
    """

            let ctx = getContext projectOptions source
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [ Range(3, 5) ]
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-002" [ Range(3, 5) ]
        }

    [<Test>]
    let ``get range scoped ignore handles nested ignores`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer: ignore-region-start IONIDE-001
    // fsharpanalyzer: ignore-region-start IONIDE-002
    let x = 1
    // fsharpanalyzer: ignore-region-end
    // fsharpanalyzer: ignore-region-end
    """

            let ctx = getContext projectOptions source
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-001" [ Range(3, 7) ]
            ctx.AnalyzerIgnoreRanges |> tryCompareRanges "IONIDE-002" [ Range(4, 6) ]
        }

    [<Test>]
    let ``ignores unclosed range scoped ignore`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer: ignore-region-start IONIDE-001
    let x = 1
    """

            let ctx = getContext projectOptions source
            Assert.That(ctx.AnalyzerIgnoreRanges, Is.Empty)
        }

    [<Test>]
    let ``ignores unopened range scoped ignore`` () =
        async {
            let source =
                """
    module M
    let x = 1
    // fsharpanalyzer: ignore-region-end
    """

            let ctx = getContext projectOptions source
            Assert.That(ctx.AnalyzerIgnoreRanges, Is.Empty)
        }

    [<Test>]
    let ``code can have multiple ranges for one code`` () =
        async {
            let source =
                """
    // fsharpanalyzer: ignore-file IONIDE-001
    module M
    // fsharpanalyzer: ignore-region-start IONIDE-001
    // fsharpanalyzer: ignore-line-next IONIDE-001
    let x = 1
    // fsharpanalyzer: ignore-region-end
    """

            let ctx = getContext projectOptions source

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-001" [ File; NextLine 5; Range(4, 7) ]
        }

    [<Test>]
    let ``next line ignore handles tight spacing`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer:ignore-line-next IONIDE-001
    let x = 1
    """

            let ctx = getContext projectOptions source
            Assert.That(ctx.AnalyzerIgnoreRanges, Is.Empty)
        }

    [<Test>]
    let ``next line ignore handles loose spacing`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer     :      ignore-line-next     IONIDE-001
    let x = 1
    """

            let ctx = getContext projectOptions source
            Assert.That(ctx.AnalyzerIgnoreRanges, Is.Empty)
        }

    [<Test>]
    let ``next line, multi-code ignore handles tight spacing`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer:ignore-line-next IONIDE-001,IONIDE-002
    let x = 1
    """

            let ctx = getContext projectOptions source
            Assert.That(ctx.AnalyzerIgnoreRanges, Is.Empty)
        }

    [<Test>]
    let ``next line, multi-code ignore handles loose spacing`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer   :     ignore-line-next    IONIDE-001   ,    IONIDE-002
    let x = 1
    """

            let ctx = getContext projectOptions source
            Assert.That(ctx.AnalyzerIgnoreRanges, Is.Empty)
        }

    [<Test>]
    let ``file ignore handles tight spacing`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer:ignore-file IONIDE-001
    let x = 1
    """

            let ctx = getContext projectOptions source
            Assert.That(ctx.AnalyzerIgnoreRanges, Is.Empty)
        }

    [<Test>]
    let ``file ignore handles loose spacing`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer    :    ignore-file     IONIDE-001
    let x = 1
    """

            let ctx = getContext projectOptions source
            Assert.That(ctx.AnalyzerIgnoreRanges, Is.Empty)
        }

    [<Test>]
    let ``range ignore handles tight spacing`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer:ignore-region-start IONIDE-001
    let x = 1
    // fsharpanalyzer:ignore-region-end
    """

            let ctx = getContext projectOptions source
            Assert.That(ctx.AnalyzerIgnoreRanges, Is.Empty)
        }

    [<Test>]
    let ``range ignore handles loose spacing`` () =
        async {
            let source =
                """
    module M
    // fsharpanalyzer      :   ignore-region-start     IONIDE-001
    let x = 1
    // fsharpanalyzer    :    ignore-region-start
    """

            let ctx = getContext projectOptions source
            Assert.That(ctx.AnalyzerIgnoreRanges, Is.Empty)
        }

module ClientTests =

    module RunAnalyzersSafelyTests =

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
        let ``run analyzers safely captures messages`` () =
            async {
                let source =
                    """
    module M

    let notUsed() =
        let option : Option<int> = None
        option.Value
            """

                let ctx = getContext projectOptions source
                let client = new Client<CliAnalyzerAttribute, _>()
                let path = System.IO.Path.GetFullPath(".")
                let stats = client.LoadAnalyzers(path)
                let! messages = client.RunAnalyzersSafely(ctx)
                
                Assert.That(stats.Analyzers, Is.Not.EqualTo 0)

                match List.tryHead messages with
                | Some message ->
                    match message.Output with
                    | Ok msgs -> Assert.That(msgs, Is.Not.Empty)
                    | Error ex -> Assert.Fail(sprintf "Expected messages but got exception: %A" ex)
                | None -> Assert.Fail("Expected at least one analyzer result")
            }

        [<Test>]
        let ``run analyzer safely ignores next line comment properly`` () =
            async {
                let source =
                    """
    module M

    let notUsed() =
        let option : Option<int> = None
        // fsharpanalyzer: ignore-line-next OV001
        option.Value
            """

                let ctx = getContext projectOptions source
                let client = new Client<CliAnalyzerAttribute, _>()
                let path = System.IO.Path.GetFullPath(".")
                let stats = client.LoadAnalyzers(path)
                let! messages = client.RunAnalyzersSafely(ctx)
                
                Assert.That(stats.Analyzers, Is.Not.EqualTo 0)

                match List.tryHead messages with
                | Some message ->
                    match message.Output with
                    | Ok msgs -> Assert.That(msgs, Is.Empty)
                    | Error ex -> Assert.Fail(sprintf "Expected no messages but got exception: %A" ex)
                | None -> Assert.Fail("Expected at least one analyzer result")
            }

        [<Test>]
        let ``run analyzer safely ignores current line comment properly`` () =
            async {
                let source =
                    """
    module M

    let notUsed() =
        let option : Option<int> = None
        option.Value // fsharpanalyzer: ignore-line OV001
            """

                let ctx = getContext projectOptions source
                let client = new Client<CliAnalyzerAttribute, _>()
                let path = System.IO.Path.GetFullPath(".")
                let stats = client.LoadAnalyzers(path)
                let! messages = client.RunAnalyzersSafely(ctx)

                Assert.That(stats.Analyzers, Is.Not.EqualTo 0)
                
                match List.tryHead messages with
                | Some message ->
                    match message.Output with
                    | Ok msgs -> Assert.That(msgs, Is.Empty)
                    | Error ex -> Assert.Fail(sprintf "Expected no messages but got exception: %A" ex)
                | None -> Assert.Fail("Expected at least one analyzer result")
            }
        
        [<Test>]
        let ``run analyzer safely ignores file comment properly`` () =
            async {
                let source =
                    """
    // fsharpanalyzer: ignore-file OV001
    module M

    let notUsed() =
        let option : Option<int> = None
        option.Value
            """

                let ctx = getContext projectOptions source
                let client = new Client<CliAnalyzerAttribute, _>()
                let path = System.IO.Path.GetFullPath(".")
                let stats = client.LoadAnalyzers(path)
                let! messages = client.RunAnalyzersSafely(ctx)
                
                Assert.That(stats.Analyzers, Is.Not.EqualTo 0)
                
                match List.tryHead messages with
                | Some message ->
                    match message.Output with
                    | Ok msgs -> Assert.That(msgs, Is.Empty)
                    | Error ex -> Assert.Fail(sprintf "Expected no messages but got exception: %A" ex)
                | None -> Assert.Fail("Expected at least one analyzer result")
            }
        
        [<Test>]
        let ``run analyzer safely ignores range comment properly`` () =
            async {
                let source =
                    """
    // fsharpanalyzer: ignore-region-start OV001
    module M

    let notUsed() =
        let option : Option<int> = None
        option.Value
    // fsharpanalyzer: ignore-region-end
            """

                let ctx = getContext projectOptions source
                let client = new Client<CliAnalyzerAttribute, _>()
                let path = System.IO.Path.GetFullPath(".")
                let stats = client.LoadAnalyzers(path)
                let! messages = client.RunAnalyzersSafely(ctx)
                
                Assert.That(stats.Analyzers, Is.Not.EqualTo 0)
                
                match List.tryHead messages with
                | Some message ->
                    match message.Output with
                    | Ok msgs -> Assert.That(msgs, Is.Empty)
                    | Error ex -> Assert.Fail(sprintf "Expected no messages but got exception: %A" ex)
                | None -> Assert.Fail("Expected at least one analyzer result")
            }

    module RunAnalyzersTests =

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
        let ``run analyzers captures messages`` () =
            async {
                let source =
                    """
    module M

    let notUsed() =
        let option : Option<int> = None
        option.Value
            """

                let ctx = getContext projectOptions source
                let client = new Client<CliAnalyzerAttribute, _>()
                let path = System.IO.Path.GetFullPath(".")
                let stats = client.LoadAnalyzers(path)
                let! messages = client.RunAnalyzers(ctx)
                
                Assert.That(stats.Analyzers, Is.Not.EqualTo 0)
                Assert.That(messages, Is.Not.Empty)
            }

        [<Test>]
        let ``run analyzer ignores next line comment properly`` () =
            async {
                let source =
                    """
    module M

    let notUsed() =
        let option : Option<int> = None
        // fsharpanalyzer: ignore-line-next OV001
        option.Value
            """

                let ctx = getContext projectOptions source
                let client = new Client<CliAnalyzerAttribute, _>()
                let path = System.IO.Path.GetFullPath(".")
                let stats = client.LoadAnalyzers(path)
                let! messages = client.RunAnalyzers(ctx)
                
                Assert.That(stats.Analyzers, Is.Not.EqualTo 0)
                Assert.That(messages, Is.Empty)
            }

        [<Test>]
        let ``run analyzer ignores current line comment properly`` () =
            async {
                let source =
                    """
    module M

    let notUsed() =
        let option : Option<int> = None
        option.Value // fsharpanalyzer: ignore-line OV001
            """

                let ctx = getContext projectOptions source
                let client = new Client<CliAnalyzerAttribute, _>()
                let path = System.IO.Path.GetFullPath(".")
                let stats = client.LoadAnalyzers(path)
                let! messages = client.RunAnalyzers(ctx)

                Assert.That(stats.Analyzers, Is.Not.EqualTo 0)
                Assert.That(messages, Is.Empty)
            }
        
        [<Test>]
        let ``run analyzer ignores file comment properly`` () =
            async {
                let source =
                    """
    // fsharpanalyzer: ignore-file OV001
    module M

    let notUsed() =
        let option : Option<int> = None
        option.Value
            """

                let ctx = getContext projectOptions source
                let client = new Client<CliAnalyzerAttribute, _>()
                let path = System.IO.Path.GetFullPath(".")
                let stats = client.LoadAnalyzers(path)
                let! messages = client.RunAnalyzers(ctx)
                
                Assert.That(stats.Analyzers, Is.Not.EqualTo 0)
                Assert.That(messages, Is.Empty)
            }
        
        [<Test>]
        let ``run analyzer ignores range comment properly`` () =
            async {
                let source =
                    """
    // fsharpanalyzer: ignore-region-start OV001
    module M

    let notUsed() =
        let option : Option<int> = None
        option.Value
    // fsharpanalyzer: ignore-region-end
            """

                let ctx = getContext projectOptions source
                let client = new Client<CliAnalyzerAttribute, _>()
                let path = System.IO.Path.GetFullPath(".")
                let stats = client.LoadAnalyzers(path)
                let! messages = client.RunAnalyzers(ctx)
                
                Assert.That(stats.Analyzers, Is.Not.EqualTo 0)
                Assert.That(messages, Is.Empty)
            }