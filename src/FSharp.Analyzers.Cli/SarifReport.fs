module FSharp.Analyzers.Cli.SarifReport

open System
open System.IO
open Microsoft.CodeAnalysis.Sarif
open Microsoft.CodeAnalysis.Sarif.Writers
open Microsoft.Extensions.Logging
open FSharp.Compiler.Text
open FSharp.Analyzers.SDK

let private fileLocation (codeRoot: Uri) (fileName: string) =
    let physicalLocation = PhysicalLocation()

    physicalLocation.ArtifactLocation <-
        let al = ArtifactLocation()
        al.Uri <- codeRoot.MakeRelativeUri(Uri(fileName))
        al

    let location = Location()
    location.PhysicalLocation <- physicalLocation
    location

let private location (codeRoot: Uri) (range: range) =
    let location = fileLocation codeRoot range.FileName

    location.PhysicalLocation.Region <-
        let r = Region()
        r.StartLine <- range.StartLine
        r.StartColumn <- range.StartColumn + 1
        r.EndLine <- range.EndLine
        r.EndColumn <- range.EndColumn + 1
        r

    location

let writeReport
    (logger: ILogger)
    (results: AnalyzerMessage list)
    (typeCheckFailures: TypeCheckFailure list)
    (analyzerFailures: AnalyzerFailure list)
    (codeRoot: Uri)
    (report: string)
    =
    try
        // Construct full path to ensure path separators are normalized.
        let report = Path.GetFullPath report
        // Ensure the parent directory exists
        let reportFile = FileInfo(report)
        reportFile.Directory.Create()

        let driver = ToolComponent()
        driver.Name <- "Ionide.Analyzers.Cli"
        driver.InformationUri <- Uri("https://ionide.io/FSharp.Analyzers.SDK/")

        driver.Version <-
            string<Version>(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version)

        let tool = Tool()
        tool.Driver <- driver
        let run = Run()
        run.Tool <- tool

        use sarifLogger =
            new SarifLogger(
                report,
                logFilePersistenceOptions =
                    (FilePersistenceOptions.PrettyPrint
                     ||| FilePersistenceOptions.ForceOverwrite),
                run = run,
                levels = BaseLogger.ErrorWarningNote,
                kinds = BaseLogger.Fail,
                closeWriterOnDispose = true
            )

        sarifLogger.AnalysisStarted()

        for analyzerResult in results do
            let reportDescriptor = ReportingDescriptor()
            reportDescriptor.Id <- analyzerResult.Message.Code
            reportDescriptor.Name <- analyzerResult.Message.Message

            analyzerResult.ShortDescription
            |> Option.iter (fun shortDescription ->
                reportDescriptor.ShortDescription <-
                    MultiformatMessageString(shortDescription, shortDescription, dict [])
            )

            analyzerResult.HelpUri
            |> Option.iter (fun helpUri -> reportDescriptor.HelpUri <- Uri(helpUri))

            let result = Result()
            result.RuleId <- reportDescriptor.Id

            result.Level <-
                match analyzerResult.Message.Severity with
                | Severity.Info -> FailureLevel.Note
                | Severity.Hint -> FailureLevel.Note
                | Severity.Warning -> FailureLevel.Warning
                | Severity.Error -> FailureLevel.Error

            let msg = Message()
            msg.Text <- analyzerResult.Message.Message
            result.Message <- msg

            result.Locations <- [| location codeRoot analyzerResult.Message.Range |]

            sarifLogger.Log(reportDescriptor, result, System.Nullable())

        // A notification at error level also sets `executionSuccessful` to false on the invocation,
        // so a reader of the report can tell the run only covered part of the code.
        for typeCheckFailure in typeCheckFailures do
            for diagnostic in typeCheckFailure.Errors do
                let notification = Notification()
                notification.Level <- FailureLevel.Error

                // The compiler error code is not a rule of this tool, so it goes in the message instead of a descriptor,
                // which would have to refer to a notification declared on the driver.
                // An error without a source location, such as one in the compiler options, has no file to point to.
                let hasLocation = Path.IsPathRooted diagnostic.Range.FileName

                let consequence =
                    if hasLocation then
                        "analyzers using the typed tree cannot see the code this error is in"
                    else
                        "analyzers using the typed tree may not see all of its code"

                let msg = Message()

                msg.Text <-
                    $"%s{typeCheckFailure.ProjectFileName} did not type check, %s{consequence}. %s{diagnostic.ErrorNumberText}: %s{diagnostic.Message}"

                notification.Message <- msg

                if hasLocation then
                    notification.Locations <- [| location codeRoot diagnostic.Range |]

                sarifLogger.LogToolNotification(notification, null)

        for analyzerFailure in analyzerFailures do
            let notification = Notification()
            notification.Level <- FailureLevel.Error

            let msg = Message()

            msg.Text <-
                $"Analyzer %s{analyzerFailure.AnalyzerName} failed, its messages for this file are missing: %s{analyzerFailure.Exception.Message}"

            notification.Message <- msg
            notification.Exception <- ExceptionData.Create analyzerFailure.Exception

            if Path.IsPathRooted analyzerFailure.FileName then
                notification.Locations <- [| fileLocation codeRoot analyzerFailure.FileName |]

            sarifLogger.LogToolNotification(notification, null)

        sarifLogger.AnalysisStopped(RuntimeConditions.None)

        sarifLogger.Dispose()
    with ex ->
        logger.LogError(ex, "Could not write sarif to {report}", report)
        logger.LogInformation("{0}", ex)
