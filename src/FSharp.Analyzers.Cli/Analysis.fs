module FSharp.Analyzers.Cli.Analysis

open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Text
open GlobExpressions
open Microsoft.Extensions.Logging
open FSharp.Analyzers.SDK
open FSharp.Analyzers.Cli.SeverityMapping
open FSharp.Analyzers.Cli.ProjectLoading

/// The outcome of running the analyzers on one project or script.
type ProjectAnalysis =
    {
        /// Set when the project or script did not type check, so the results may be incomplete.
        TypeCheckFailure: TypeCheckFailure option
        /// The messages of each analyzer per file, or how the analyzer failed on that file.
        Results: Result<AnalyzerMessage list, AnalyzerFailure> list
    }

let runProject
    (logger: ILogger)
    (checker: FSharpChecker)
    (client: Client<CliAnalyzerAttribute, CliContext>)
    (fsharpOptions: FSharpProjectOptions)
    (loadErrors: FSharpDiagnostic list)
    (excludeIncludeFiles: Choice<Glob list, Glob list>)
    (mappings: SeverityMappings)
    : Async<ProjectAnalysis>
    =
    async {
        logger.LogInformation("Checking project {0}", fsharpOptions.ProjectFileName)
        let! checkProjectResults = checker.ParseAndCheckProject(fsharpOptions)
        let analyzerOptions = BackgroundCompilerOptions fsharpOptions

        // Every file is checked, also the ones the file filters leave out: those filters choose what the analyzers run on,
        // not which type errors count. An excluded file that does not type check still leaves code out of the typed tree.
        let! checkedFiles =
            fsharpOptions.SourceFiles
            |> Array.map (fun fileName ->
                async {
                    logger.LogDebug("Checking file {0}", fileName)

                    // Since we did ParseAndCheckProject, we can be sure that the file is in the project.
                    // See https://fsharp.github.io/fsharp-compiler-docs/fcs/project.html for more information.
                    let! parseAndCheckResults =
                        checker.GetBackgroundCheckResultsForFileInProject(fileName, fsharpOptions)

                    return fileName, parseAndCheckResults
                }
            )
            |> Async.Parallel

        // A type error is replaced with an error recovery node in the typed tree, so any analyzer looking
        // at that part of the tree silently reports nothing. Surface the errors instead of looking clean.
        // An assembly reference that cannot be resolved is only reported in the check results of a file,
        // not in the ones of the project, and a file reports the type errors the project already has.
        let typeCheckErrors =
            [
                yield! loadErrors
                yield! checkProjectResults.Diagnostics
                for _, (_, checkFileResults) in checkedFiles do
                    yield! checkFileResults.Diagnostics
            ]
            |> List.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)
            |> List.distinctBy (fun d -> d.Range, d.ErrorNumber, d.Message)
            |> List.sortBy (fun d -> d.Range.FileName, d.Range.StartLine, d.Range.StartColumn)

        let typeCheckFailure =
            match typeCheckErrors with
            | [] -> None
            | errors ->
                logger.LogWarning(
                    "{0} did not type check, results may be incomplete. Analyzers using the typed tree cannot see code that failed to type check.",
                    fsharpOptions.ProjectFileName
                )

                for d in errors do
                    // An error without a source location, such as one in the compiler options, has no file to point to.
                    if Path.IsPathRooted d.Range.FileName then
                        logger.LogWarning(
                            "{0}{1}: {2}: {3}",
                            d.Range.FileName,
                            d.Range,
                            d.ErrorNumberText,
                            d.Message
                        )
                    else
                        logger.LogWarning("{0}: {1}", d.ErrorNumberText, d.Message)

                Some
                    {
                        ProjectFileName = fsharpOptions.ProjectFileName
                        Errors = errors
                    }

        // The type errors are logged before the analyzers run, so an analyzer that throws does not hide them.
        let! resultsPerFile =
            checkedFiles
            |> Array.choose (fun (fileName, parseAndCheckResults) ->
                let included =
                    match excludeIncludeFiles with
                    | Choice1Of2 excludeFiles ->
                        match
                            excludeFiles
                            |> List.tryFind (fun g -> g.IsMatch fileName)
                        with
                        | Some g ->
                            logger.LogInformation(
                                "Ignoring file {0} for pattern {1}",
                                fileName,
                                g.Pattern
                            )

                            false
                        | None -> true
                    | Choice2Of2 includeFiles ->
                        match
                            includeFiles
                            |> List.tryFind (fun g -> g.IsMatch fileName)
                        with
                        | Some g ->
                            logger.LogInformation(
                                "Including file {0} for pattern {1}",
                                fileName,
                                g.Pattern
                            )

                            true
                        | None -> false

                if not included then
                    None
                else

                Some(
                    async {
                        let! fileContent =
                            File.ReadAllTextAsync fileName
                            |> Async.AwaitTask

                        let sourceText = SourceText.ofString fileContent

                        let ctx =
                            Utils.createContext
                                checkProjectResults
                                fileName
                                sourceText
                                parseAndCheckResults
                                analyzerOptions

                        logger.LogInformation("Running analyzers for {0}", ctx.FileName)
                        let! results = client.RunAnalyzers ctx

                        // An analyzer that throws only loses its own messages for this file, the other analyzers keep theirs.
                        return
                            [
                                for result in results do
                                    match result.Output with
                                    | Ok messages ->
                                        messages
                                        |> List.map (mapMessageToSeverity mappings)
                                        |> Ok
                                    | Error error ->
                                        logger.LogError(
                                            error,
                                            "Analyzer {0} failed on {1}, its messages for this file are missing.",
                                            result.AnalyzerName,
                                            fileName
                                        )

                                        Error
                                            {
                                                AnalyzerName = result.AnalyzerName
                                                FileName = fileName
                                                Exception = error
                                            }
                            ]
                    }
                )
            )
            |> Async.Parallel

        let results = List.concat resultsPerFile

        return
            {
                TypeCheckFailure = typeCheckFailure
                Results = results
            }
    }

let runFscArgs
    (logger: ILogger)
    (checker: FSharpChecker)
    (client: Client<CliAnalyzerAttribute, CliContext>)
    (fscArgs: string)
    (excludeIncludeFiles: Choice<Glob list, Glob list>)
    (mappings: SeverityMappings)
    =
    if String.IsNullOrWhiteSpace fscArgs then
        logger.LogError("Empty --fsc-args were passed!")
        exit (int ExitErrorCodes.EmptyFscArgs)
    else

    let fscArgs = fscArgs.Split(';', StringSplitOptions.RemoveEmptyEntries)

    let sourceFiles =
        fscArgs
        |> Array.choose (fun (argument: string) ->
            // We make an absolute path because the sarif report cannot deal properly with relative path.
            let path = Path.Combine(Directory.GetCurrentDirectory(), argument)

            if
                not (isFSharpFile path)
                || not (File.Exists path)
            then
                None
            else
                Some path
        )

    let otherOptions =
        fscArgs
        |> Array.filter (fun line ->
            not (isFSharpFile line)
            && not (isWarnAsError line)
        )

    let projectOptions =
        {
            ProjectFileName = "Project"
            ProjectId = None
            SourceFiles = sourceFiles
            OtherOptions = otherOptions
            ReferencedProjects = [||]
            IsIncompleteTypeCheckEnvironment = false
            UseScriptResolutionRules = false
            LoadTime = DateTime.Now
            UnresolvedReferences = None
            OriginalLoadReferences = []
            Stamp = None
        }

    runProject logger checker client projectOptions [] excludeIncludeFiles mappings
