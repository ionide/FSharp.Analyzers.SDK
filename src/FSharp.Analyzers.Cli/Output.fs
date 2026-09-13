module FSharp.Analyzers.Cli.Output

open System
open Microsoft.Extensions.Logging
open FSharp.Analyzers.SDK
open FSharp.Analyzers.Cli.CustomLogging

[<RequireQualifiedAccess>]
type OutputFormat =
    | Default
    | GitHub

let parseOutputFormat =
    function
    | "github" -> Ok OutputFormat.GitHub
    | "default" -> Ok OutputFormat.Default
    | other -> Error $"Unknown output format: %s{other}."

let printMessagesInDefaultFormat (logger: ILogger) (msgs: AnalyzerMessage list) =

    let severityToLogLevel =
        Map.ofArray
            [|
                Severity.Error, LogLevel.Error
                Severity.Warning, LogLevel.Warning
                Severity.Info, LogLevel.Information
                Severity.Hint, LogLevel.Trace
            |]

    if List.isEmpty msgs then
        logger.LogInformation("No messages found from the analyzer(s)")

    use factory =
        LoggerFactory.Create(fun builder ->
            builder
                .AddCustomFormatter(fun options -> options.UseAnalyzersMsgStyle <- true)
                .SetMinimumLevel(LogLevel.Trace)
            |> ignore
        )

    let msgLogger = factory.CreateLogger("")

    msgs
    |> List.iter (fun analyzerMessage ->
        let m = analyzerMessage.Message

        msgLogger.Log(
            severityToLogLevel[m.Severity],
            "{0}({1},{2}): {3} {4} : {5}",
            m.Range.FileName,
            m.Range.StartLine,
            m.Range.StartColumn,
            m.Severity.ToString(),
            m.Code,
            m.Message
        )
    )

    ()

let printMessagesInGitHubFormat (logger: ILogger) (codeRoot: Uri) (msgs: AnalyzerMessage list) =
    let severityToLogLevel =
        Map.ofArray
            [|
                Severity.Error, LogLevel.Error
                Severity.Warning, LogLevel.Warning
                Severity.Info, LogLevel.Information
                Severity.Hint, LogLevel.Trace
            |]

    let severityToGitHubAnnotationType =
        Map.ofArray
            [|
                Severity.Error, "error"
                Severity.Warning, "warning"
                Severity.Info, "notice"
                Severity.Hint, "notice"
            |]

    if List.isEmpty msgs then
        logger.LogInformation("No messages found from the analyzer(s)")

    use factory =
        LoggerFactory.Create(fun builder ->
            builder
                .AddCustomFormatter(fun options -> options.UseAnalyzersMsgStyle <- true)
                .SetMinimumLevel(LogLevel.Trace)
            |> ignore
        )

    // No category name because GitHub needs the annotation type to be the first
    // element on each line.
    let msgLogger = factory.CreateLogger("")

    msgs
    |> List.iter (fun analyzerMessage ->
        let m = analyzerMessage.Message

        // We want file names to be relative to the repository so GitHub will recognize them.
        // GitHub also only understands Unix-style directory separators.
        let relativeFileName =
            codeRoot.MakeRelativeUri(Uri(m.Range.FileName))
            |> _.OriginalString

        msgLogger.Log(
            severityToLogLevel[m.Severity],
            "::{0} file={1},line={2},endLine={3},col={4},endColumn={5},title={6} ({7})::{8}: {9}",
            severityToGitHubAnnotationType[m.Severity],
            relativeFileName,
            m.Range.StartLine,
            m.Range.EndLine,
            m.Range.StartColumn,
            m.Range.EndColumn,
            analyzerMessage.Name,
            m.Code,
            m.Severity.ToString(),
            m.Message
        )
    )

    ()
