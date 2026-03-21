module OptionAnalyzer.Test

#nowarn "57"

open FSharp.Compiler.CodeAnalysis
open NUnit.Framework
open FSharp.Compiler.Text
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.Testing
open OptionAnalyzer.TestHelpers

let mutable projectOptions: FSharpProjectOptions = FSharpProjectOptions.zero

[<OneTimeSetUp>]
let Setup () =
    task {
        let! opts = mkTestProjectOptions ()
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

        Assert.IsTrue(
            msgs
            |> List.contains expectedMsg
        )
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

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-001" [ NextLine 3 ]
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

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-001" [ NextLine 3 ]

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-002" [ NextLine 3 ]
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

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-001" [ CurrentLine 3 ]
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

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-001" [ CurrentLine 3 ]

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-002" [ CurrentLine 3 ]
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

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-001" [ File ]
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

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-001" [ File ]

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-002" [ File ]

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-003" [ File ]
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

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-001" [ Range(3, 5) ]
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

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-001" [ Range(3, 5) ]

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-002" [ Range(3, 5) ]
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

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-001" [ Range(3, 7) ]

            ctx.AnalyzerIgnoreRanges
            |> tryCompareRanges "IONIDE-002" [ Range(4, 6) ]
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
            |> tryCompareRanges
                "IONIDE-001"
                [
                    File
                    NextLine 5
                    Range(4, 7)
                ]
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

        let mutable projectOptions: FSharpProjectSnapshot = FSharpProjectSnapshot.zero

        let getContext snapshot source =
            let file = { FileName = "A.fs"; Source = source }
            getContextFor (TransparentCompilerOptions snapshot) [ file ] file

        [<OneTimeSetUp>]
        let Setup () =
            task {
                let! opts = mkTestProjectSnapshot ()
                projectOptions <- opts
            }

        [<Test>]
        let ``run analyzers safely captures messages`` () =
            async {
                let! ctx =
                    getContext projectOptions ClientTestSources.optionValue
                    |> Async.AwaitTask

                let client, stats = loadAnalyzers ()
                let! messages = client.RunAnalyzersSafely(ctx)

                Assert.That(stats.AnalyzerNames, Does.Contain "OptionAnalyzer")

                messages
                |> assertSafeResult false
            }

        [<Test>]
        let ``run analyzer safely ignores next line comment properly`` () =
            async {
                let! ctx =
                    getContext projectOptions ClientTestSources.ignoreNextLine
                    |> Async.AwaitTask

                let client, stats = loadAnalyzers ()
                let! messages = client.RunAnalyzersSafely(ctx)

                Assert.That(stats.AnalyzerNames, Does.Contain "OptionAnalyzer")

                messages
                |> assertSafeResult true
            }

        [<Test>]
        let ``run analyzer safely ignores current line comment properly`` () =
            async {
                let! ctx =
                    getContext projectOptions ClientTestSources.ignoreCurrentLine
                    |> Async.AwaitTask

                let client, stats = loadAnalyzers ()
                let! messages = client.RunAnalyzersSafely(ctx)

                Assert.That(stats.AnalyzerNames, Does.Contain "OptionAnalyzer")

                messages
                |> assertSafeResult true
            }

        [<Test>]
        let ``run analyzer safely ignores file comment properly`` () =
            async {
                let! ctx =
                    getContext projectOptions ClientTestSources.ignoreFile
                    |> Async.AwaitTask

                let client, stats = loadAnalyzers ()
                let! messages = client.RunAnalyzersSafely(ctx)

                Assert.That(stats.AnalyzerNames, Does.Contain "OptionAnalyzer")

                messages
                |> assertSafeResult true
            }

        [<Test>]
        let ``run analyzer safely ignores range comment properly`` () =
            async {
                let! ctx =
                    getContext projectOptions ClientTestSources.ignoreRange
                    |> Async.AwaitTask

                let client, stats = loadAnalyzers ()
                let! messages = client.RunAnalyzersSafely(ctx)

                Assert.That(stats.AnalyzerNames, Does.Contain "OptionAnalyzer")

                messages
                |> assertSafeResult true
            }

    module RunAnalyzersTests =

        let mutable projectOptions: FSharpProjectOptions = FSharpProjectOptions.zero

        [<OneTimeSetUp>]
        let Setup () =
            task {
                let! opts = mkTestProjectOptions ()
                projectOptions <- opts
            }

        [<Test>]
        let ``run analyzers captures messages`` () =
            async {
                let ctx = getContext projectOptions ClientTestSources.optionValue
                let client, stats = loadAnalyzers ()
                let! messages = client.RunAnalyzers(ctx)

                Assert.That(stats.AnalyzerNames, Does.Contain "OptionAnalyzer")
                Assert.That(messages, Is.Not.Empty)
            }

        [<Test>]
        let ``run analyzer ignores next line comment properly`` () =
            async {
                let ctx = getContext projectOptions ClientTestSources.ignoreNextLine
                let client, stats = loadAnalyzers ()
                let! messages = client.RunAnalyzers(ctx)

                Assert.That(stats.AnalyzerNames, Does.Contain "OptionAnalyzer")
                Assert.That(messages, Is.Empty)
            }

        [<Test>]
        let ``run analyzer ignores current line comment properly`` () =
            async {
                let ctx = getContext projectOptions ClientTestSources.ignoreCurrentLine
                let client, stats = loadAnalyzers ()
                let! messages = client.RunAnalyzers(ctx)

                Assert.That(stats.AnalyzerNames, Does.Contain "OptionAnalyzer")
                Assert.That(messages, Is.Empty)
            }

        [<Test>]
        let ``run analyzer ignores file comment properly`` () =
            async {
                let ctx = getContext projectOptions ClientTestSources.ignoreFile
                let client, stats = loadAnalyzers ()
                let! messages = client.RunAnalyzers(ctx)

                Assert.That(stats.AnalyzerNames, Does.Contain "OptionAnalyzer")
                Assert.That(messages, Is.Empty)
            }

        [<Test>]
        let ``run analyzer ignores range comment properly`` () =
            async {
                let ctx = getContext projectOptions ClientTestSources.ignoreRange
                let client, stats = loadAnalyzers ()
                let! messages = client.RunAnalyzers(ctx)

                Assert.That(stats.AnalyzerNames, Does.Contain "OptionAnalyzer")
                Assert.That(messages, Is.Empty)
            }
