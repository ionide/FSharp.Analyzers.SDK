module FSharp.Analyzers.Cli.Analysis

open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open GlobExpressions
open Microsoft.Extensions.Logging
open FSharp.Analyzers.SDK
open FSharp.Analyzers.Cli.SeverityMapping
open FSharp.Analyzers.Cli.ProjectLoading

let runProject
    (logger: ILogger)
    (checker: FSharpChecker)
    (client: Client<CliAnalyzerAttribute, CliContext>)
    (fsharpOptions: FSharpProjectOptions)
    (excludeIncludeFiles: Choice<Glob list, Glob list>)
    (mappings: SeverityMappings)
    : Async<Result<AnalyzerMessage list, AnalysisFailure> list>
    =
    async {
        logger.LogInformation("Checking project {0}", fsharpOptions.ProjectFileName)
        let! checkProjectResults = checker.ParseAndCheckProject(fsharpOptions)
        let analyzerOptions = BackgroundCompilerOptions fsharpOptions

        let! messagesPerAnalyzer =
            fsharpOptions.SourceFiles
            |> Array.filter (fun file ->
                match excludeIncludeFiles with
                | Choice1Of2 excludeFiles ->
                    match
                        excludeFiles
                        |> List.tryFind (fun g -> g.IsMatch file)
                    with
                    | Some g ->
                        logger.LogInformation("Ignoring file {0} for pattern {1}", file, g.Pattern)
                        false
                    | None -> true
                | Choice2Of2 includeFiles ->
                    match
                        includeFiles
                        |> List.tryFind (fun g -> g.IsMatch file)
                    with
                    | Some g ->
                        logger.LogInformation(
                            "Including file {0} for pattern {1}",
                            file,
                            g.Pattern
                        )

                        true
                    | None -> false
            )
            |> Array.map (fun fileName ->
                async {
                    let! fileContent =
                        File.ReadAllTextAsync fileName
                        |> Async.AwaitTask

                    let sourceText = SourceText.ofString fileContent
                    logger.LogDebug("Checking file {0}", fileName)

                    // Since we did ParseAndCheckProject, we can be sure that the file is in the project.
                    // See https://fsharp.github.io/fsharp-compiler-docs/fcs/project.html for more information.
                    let! parseAndCheckResults =
                        checker.GetBackgroundCheckResultsForFileInProject(fileName, fsharpOptions)

                    let ctx =
                        Utils.createContext
                            checkProjectResults
                            fileName
                            sourceText
                            parseAndCheckResults
                            analyzerOptions

                    logger.LogInformation("Running analyzers for {0}", ctx.FileName)
                    let! results = client.RunAnalyzers ctx
                    return Ok results
                }

            )
            |> Async.Parallel

        return
            messagesPerAnalyzer
            |> Seq.map (fun messages ->
                match messages with
                | Error e -> Error e
                | Ok messages ->
                    messages
                    |> List.map (mapMessageToSeverity mappings)
                    |> Ok
            )
            |> Seq.toList
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
        |> Array.filter (fun line -> not (isFSharpFile line))

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

    runProject logger checker client projectOptions excludeIncludeFiles mappings
