module FSharp.Analyzers.Cli.Tests.AnalysisTests

open System
open System.IO
open NUnit.Framework
open Microsoft.Extensions.Logging
open GlobExpressions
open Ionide.ProjInfo
open FSharp.Analyzers.SDK
open FSharp.Analyzers.Cli.Analysis
open FSharp.Analyzers.Cli.ProjectLoading
open FSharp.Analyzers.Cli.SeverityMapping
open FSharp.Analyzers.Cli.Tests.AnalyzerLoadingTests

let private toolsPath =
    lazy (Init.init (DirectoryInfo Environment.CurrentDirectory) None)

let private noMappings =
    {
        TreatAsHint = []
        TreatAsInfo = []
        TreatAsWarning = []
        TreatAsError = []
    }

/// A logger that keeps every message it gets.
let private recordingLogger (messages: ResizeArray<string>) =
    { new ILogger with
        member _.BeginScope _ = Unchecked.defaultof<IDisposable>
        member _.IsEnabled _ = true

        member _.Log(_, _, state, ex, formatter) =
            lock messages (fun () -> messages.Add(formatter.Invoke(state, ex)))
    }

// A #load and a #r that cannot be resolved, and code that uses the missing assembly.
// The load error comes from loading the script, the unresolved #r only from the check of the file,
// and the type errors from both the check of the project and the one of the file.
let private brokenScript =
    "#load \"does-not-exist.fsx\"\n#r \"missing/Library.dll\"\nlet y: Library.Thing = Library.make ()\n"

/// Writes the source to a temporary script, loads it and runs the analyzers of the client on the files the filters leave in.
let private runScriptWithFilters
    (excludeIncludeFiles: Choice<Glob list, Glob list>)
    (logger: ILogger)
    (client: Client<CliAnalyzerAttribute, CliContext>)
    (source: string)
    =
    let script = Path.Combine(Path.GetTempPath(), $"%s{Path.GetRandomFileName()}.fsx")
    File.WriteAllText(script, source)

    try
        let checker = Utils.createFCS None

        let options, loadErrors =
            loadScripts logger toolsPath.Value checker [ script ]
            |> Async.RunSynchronously
            |> Array.exactlyOne

        runProject logger checker client options loadErrors excludeIncludeFiles noMappings
        |> Async.RunSynchronously
    finally
        File.Delete script

let private runScript = runScriptWithFilters (Choice1Of2 [])

let private errorNumbers (analysis: ProjectAnalysis) =
    analysis.TypeCheckFailure
    |> Option.map (fun failure -> failure.Errors)
    |> Option.defaultValue []
    |> List.map (fun d -> d.ErrorNumber)
    |> List.sort

// See https://github.com/ionide/FSharp.Analyzers.SDK/issues/350
[<Test>]
let ``the type errors of a script combine its load, project and file errors without duplicates``
    ()
    =
    let messages = ResizeArray()
    let logger = recordingLogger messages

    let analysis =
        runScript logger (Client<CliAnalyzerAttribute, CliContext>(logger)) brokenScript

    Assert.That(
        errorNumbers analysis,
        Is.EqualTo
            [
                39
                39
                79
                84
            ]
    )

// The file filters choose what the analyzers run on, not which type errors count.
[<Test>]
let ``an excluded file still reports all of its type errors`` () =
    let messages = ResizeArray()
    let logger = recordingLogger messages

    let analysis =
        runScriptWithFilters
            (Choice1Of2 [ Glob "**/*.fsx" ])
            logger
            (Client<CliAnalyzerAttribute, CliContext>(logger))
            brokenScript

    Assert.That(analysis.Results, Is.Empty)

    Assert.That(
        errorNumbers analysis,
        Is.EqualTo
            [
                39
                39
                79
                84
            ]
    )

[<Test>]
let ``a script that type checks has no type check failure`` () =
    let messages = ResizeArray()
    let logger = recordingLogger messages

    let analysis =
        runScript logger (Client<CliAnalyzerAttribute, CliContext>(logger)) "let x = 1\n"

    Assert.That(analysis.TypeCheckFailure, Is.EqualTo None)

/// Compiles an analyzer that throws while it runs, one that throws before it returns its async,
/// and one that reports a message on every file, and gives them to f in a client that logs to messages.
let private withAnalyzers (messages: ResizeArray<string>) f =
    let analyzersDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())

    Directory.CreateDirectory analyzersDir
    |> ignore

    try
        // The exception is thrown when the async runs, not when the analyzer function returns it.
        compile
            []
            (Path.Combine(analyzersDir, "ThrowingAnalyzer.dll"))
            """
module ThrowingAnalyzer

open FSharp.Analyzers.SDK

[<CliAnalyzer "ThrowingAnalyzer">]
let throwing: Analyzer<CliContext> = fun _ -> async { return failwith "boom" }
"""

        // A method analyzer is called by reflection, and this one throws before it returns its async.
        compile
            []
            (Path.Combine(analyzersDir, "EagerThrowingAnalyzer.dll"))
            """
module EagerThrowingAnalyzer

open FSharp.Analyzers.SDK

[<CliAnalyzer "EagerThrowingAnalyzer">]
let eagerThrowing (_: CliContext) : Async<Message list> = failwith "eager boom"
"""

        compile
            []
            (Path.Combine(analyzersDir, "MessageAnalyzer.dll"))
            """
module MessageAnalyzer

open FSharp.Analyzers.SDK

[<CliAnalyzer "MessageAnalyzer">]
let message: Analyzer<CliContext> =
    fun _ ->
        async {
            return
                [
                    {
                        Type = "Message"
                        Message = "A message"
                        Code = "MSG001"
                        Severity = Severity.Warning
                        Range = FSharp.Compiler.Text.Range.range0
                        Fixes = []
                    }
                ]
        }
"""

        let logger = recordingLogger messages
        let client = Client<CliAnalyzerAttribute, CliContext>(logger)
        let stats = client.LoadAnalyzers analyzersDir
        Assert.That(stats.Analyzers, Is.EqualTo 3)

        f logger client
    finally
        try
            Directory.Delete(analyzersDir, true)
        with _ ->
            ()

// Analyzers are most likely to throw on the error recovery nodes of code that did not type check.
[<Test>]
let ``an analyzer that throws keeps the type errors and the messages of the other analyzers`` () =
    let messages = ResizeArray()

    let analysis =
        withAnalyzers messages (fun logger client -> runScript logger client brokenScript)

    let codes =
        analysis.Results
        |> List.collect (
            function
            | Ok analyzerMessages -> analyzerMessages
            | Error _ -> []
        )
        |> List.map (fun m -> m.Message.Code)

    let failures =
        analysis.Results
        |> List.choose (
            function
            | Ok _ -> None
            | Error failure -> Some(failure.AnalyzerName, failure.Exception.Message)
        )
        |> List.sort

    Assert.That(codes, Is.EqualTo [ "MSG001" ])

    Assert.That(
        failures,
        Is.EqualTo
            [
                "EagerThrowingAnalyzer", "eager boom"
                "ThrowingAnalyzer", "boom"
            ]
    )

    Assert.That(
        errorNumbers analysis,
        Is.EqualTo
            [
                39
                39
                79
                84
            ]
    )

    Assert.That(messages, Has.Some.Contains "did not type check")
    Assert.That(messages, Has.Some.Contains "Analyzer ThrowingAnalyzer failed on")
