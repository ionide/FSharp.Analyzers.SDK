open System
open System.IO
open System.Runtime.Loader
open FSharp.Compiler.CodeAnalysis
open Argu
open FSharp.Analyzers.SDK
open GlobExpressions
open Microsoft.Extensions.Logging
open Ionide.ProjInfo
open FSharp.Analyzers.Cli
open FSharp.Analyzers.Cli.CustomLogging
open FSharp.Analyzers.Cli.SeverityMapping
open FSharp.Analyzers.Cli.Output
open FSharp.Analyzers.Cli.ProjectLoading
open FSharp.Analyzers.Cli.Analysis

let parser = ArgumentParser.Create<Arguments>(errorHandler = ProcessExiter())

[<EntryPoint>]
let main argv =
    let toolsPath = Init.init (DirectoryInfo Environment.CurrentDirectory) None

    let results = parser.ParseCommandLine argv

    let logLevel =
        let verbosity = results.TryGetResult <@ Verbosity @>

        match verbosity with
        | Some "d"
        | Some "detailed" -> LogLevel.Information
        | Some "diag"
        | Some "diagnostic" -> LogLevel.Debug
        | Some "n" -> LogLevel.Warning
        | Some "normal" -> LogLevel.Warning
        | None -> LogLevel.Warning
        | Some x ->
            use factory = LoggerFactory.Create(fun b -> b.AddConsole() |> ignore)

            let logger = factory.CreateLogger("")
            logger.LogError("unknown verbosity level given {0}", x)
            exit (int ExitErrorCodes.UnknownLoggerVerbosity)

    use factory =
        LoggerFactory.Create(fun builder ->
            builder
                .AddCustomFormatter(fun options ->
                    options.UseAnalyzersMsgStyle <- false
                    options.TimestampFormat <- "[HH:mm:ss.fff]"
                    options.UseUtcTimestamp <- true
                )
                .SetMinimumLevel(logLevel)
            |> ignore
        )

    let logger = factory.CreateLogger("FSharp.Analyzers.Cli")
    let checker = Utils.createFCS None

    // Set the Ionide.ProjInfo logger to use the same Microsoft.Extensions.Logging logger
    if
        logLevel
        <= LogLevel.Information
    then
        Ionide.ProjInfo.Logging.Providers
            .MicrosoftExtensionsLoggingProvider.setMicrosoftLoggerFactory
            factory

    logger.LogInformation("Running in verbose mode")

    let binlogPath =
        results.TryGetResult <@ BinLog_Path @>
        |> Option.map (
            Path.GetFullPath
            >> DirectoryInfo
        )

    AppDomain.CurrentDomain.UnhandledException.Add(fun args ->
        let ex = args.ExceptionObject :?> exn

        match ex with
        | :? FileNotFoundException as fnf when fnf.FileName.StartsWith "System.Runtime" ->
            // https://github.com/ionide/FSharp.Analyzers.SDK/issues/245
            logger.LogCritical(
                ex,
                "FSharp.Analyzers.Cli could not find {0}. If you're using a preview version of the .NET SDK, you may need to set DOTNET_ROLL_FORWARD_TO_PRERELEASE=1 in your environment before running this tool.",
                fnf.FileName
            )
        | _ -> logger.LogCritical(ex, "Unhandled exception:")

        factory.Dispose() // Flush any logs https://github.com/dotnet/extensions/issues/2395
        exit (int ExitErrorCodes.UnhandledException)
    )

    let parseSeverityCodes (argument: Quotations.Expr<string list -> Arguments>) =
        results.GetResult(argument, [])
        |> List.distinct
        |> List.map (fun input ->
            match SeverityCode.tryParse input with
            | Ok code -> code
            | Error message ->
                logger.LogError("{0}", message)
                factory.Dispose() // Flush any logs https://github.com/dotnet/extensions/issues/2395
                exit (int ExitErrorCodes.InvalidTreatAsSeverityPattern)
        )

    let severityMapping =
        {
            TreatAsHint = parseSeverityCodes <@ Treat_As_Hint @>
            TreatAsInfo = parseSeverityCodes <@ Treat_As_Info @>
            TreatAsWarning = parseSeverityCodes <@ Treat_As_Warning @>
            TreatAsError = parseSeverityCodes <@ Treat_As_Error @>
        }

    let formatSeverityCodes (codes: SeverityCode list) =
        codes
        |> List.map string
        |> String.concat ", "

    logger.LogInformation("Treat as Hints: [{0}]", formatSeverityCodes severityMapping.TreatAsHint)
    logger.LogInformation("Treat as Info: [{0}]", formatSeverityCodes severityMapping.TreatAsInfo)

    logger.LogInformation(
        "Treat as Warning: [{0}]",
        formatSeverityCodes severityMapping.TreatAsWarning
    )

    logger.LogInformation("Treat as Error: [{0}]", formatSeverityCodes severityMapping.TreatAsError)

    match severityMapping.Validate() with
    | Ok() -> ()
    | Error message ->
        logger.LogError("{0}", message)
        factory.Dispose() // Flush any logs https://github.com/dotnet/extensions/issues/2395
        exit (int ExitErrorCodes.AnalyzerListedMultipleTimesInTreatAsSeverity)

    let projOpts =
        results.GetResults <@ Project @>
        |> List.concat

    let fscArgs = results.TryGetResult <@ FSC_Args @>
    let fscArgsFile = results.TryGetResult <@ FSC_Args_File @>

    // Validate that both fsc-args and fsc-args-file are not used together
    match fscArgs, fscArgsFile with
    | Some _, Some _ ->
        logger.LogError("`--fsc-args` and `--fsc-args-file` cannot be combined.")
        exit (int ExitErrorCodes.ProjectAndFscArgs)
    | _ -> ()

    // Read fsc args from file if specified, otherwise use direct args
    let fscArgs =
        match fscArgsFile with
        | Some filePath -> Some(readFscArgsFromFile logger filePath)
        | None -> fscArgs

    let report = results.TryGetResult <@ Report @>
    let codeRoot = results.TryGetResult <@ Code_Root @>

    let cwd =
        Directory.GetCurrentDirectory()
        |> DirectoryInfo

    let scripts = resolveScriptPaths cwd (results.GetResult(<@ Script @>, []))

    let exclInclFiles =
        let excludeFiles = results.GetResult(<@ Exclude_Files @>, [])

        logger.LogInformation(
            "Exclude Files: [{0}]",
            (excludeFiles
             |> String.concat ", ")
        )

        let excludeFiles = excludeFiles |> List.map Glob

        let includeFiles = results.GetResult(<@ Include_Files @>, [])

        logger.LogInformation(
            "Include Files: [{0}]",
            (includeFiles
             |> String.concat ", ")
        )

        let includeFiles = includeFiles |> List.map Glob

        match excludeFiles, includeFiles with
        | e, [] -> Choice1Of2 e
        | [], i -> Choice2Of2 i
        | _e, i ->
            logger.LogWarning(
                "--exclude-files and --include-files are mutually exclusive, ignoring --exclude-files"
            )

            Choice2Of2 i

    let properties = MsBuildProperties.getProperties logger results

    if
        Option.isSome fscArgs
        && not properties.IsEmpty
    then
        logger.LogError("fsc-args can't be combined with MSBuild properties.")
        exit (int ExitErrorCodes.FscArgsCombinedWithMsBuildProperties)

    properties
    |> List.iter (fun (k, v) -> logger.LogInformation("Property {0}={1}", k, v))

    let outputFormat =
        results.TryGetResult <@ Output_Format @>
        |> Option.map parseOutputFormat
        |> Option.defaultValue (Ok OutputFormat.Default)
        |> Result.defaultWith (fun errMsg ->
            logger.LogError("{0} Using default output format.", errMsg)
            OutputFormat.Default
        )

    let analyzersPaths =
        results.GetResults(<@ Analyzers_Path @>)
        |> List.concat
        |> function
            | [] -> [ "packages/Analyzers" ]
            | paths -> paths
        |> List.map (fun path ->
            if Path.IsPathRooted path then
                path
            else
                Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path))
        )

    logger.LogInformation("Loading analyzers from {0}", (String.concat ", " analyzersPaths))

    let exclInclAnalyzers =
        let excludeAnalyzers = results.GetResult(<@ Exclude_Analyzers @>, [])
        let includeAnalyzers = results.GetResult(<@ Include_Analyzers @>, [])

        match excludeAnalyzers, includeAnalyzers with
        | e, [] ->
            fun (s: string) ->
                e
                |> List.map Glob
                |> List.exists (fun g -> g.IsMatch s)
            |> ExcludeFilter
        | [], i ->
            fun (s: string) ->
                i
                |> List.map Glob
                |> List.exists (fun g -> g.IsMatch s)
            |> IncludeFilter
        | _e, i ->
            logger.LogWarning(
                "--exclude-analyzers and --include-analyzers are mutually exclusive, ignoring --exclude-analyzers"
            )

            fun (s: string) ->
                i
                |> List.map Glob
                |> List.exists (fun g -> g.IsMatch s)
            |> IncludeFilter

    AssemblyLoadContext.Default.add_Resolving (fun _ctx assemblyName ->
        if
            assemblyName.Name
            <> "FSharp.Core"
        then
            null
        else

        let msg =
            $"""Could not load FSharp.Core %A{assemblyName.Version}. The expected assembly version of FSharp.Core is %A{Utils.currentFSharpCoreVersion}.
        Consider adding <PackageReference Update="FSharp.Core" Version="<CorrectVersion>" /> to your .fsproj.
        The correct version can be found over at https://www.nuget.org/packages/FSharp.Analyzers.SDK#dependencies-body-tab.
        """

        logger.LogError(msg)
        exit (int ExitErrorCodes.FSharpCoreAssemblyLoadFailed)
    )

    let client = Client<CliAnalyzerAttribute, CliContext>(logger)

    let dlls, analyzers, failedAssemblies =
        ((0, 0, 0), analyzersPaths)
        ||> List.fold (fun (accDlls, accAnalyzers, accFailed) analyzersPath ->
            let loadedDlls = client.LoadAnalyzers(analyzersPath, exclInclAnalyzers)

            (accDlls
             + loadedDlls.AnalyzerAssemblies),
            (accAnalyzers
             + loadedDlls.Analyzers),
            (accFailed
             + loadedDlls.FailedAssemblies)
        )

    logger.LogInformation("Registered {0} analyzers from {1} dlls", analyzers, dlls)

    let results =
        if analyzers = 0 then
            None
        else
            match fscArgs with
            | Some _ when
                projOpts
                |> List.isEmpty
                |> not
                ->
                logger.LogError("`--project` and `--fsc-args` cannot be combined.")
                exit (int ExitErrorCodes.ProjectAndFscArgs)
            | Some _ when scripts |> List.isEmpty |> not ->
                logger.LogError("`--script` and `--fsc-args` cannot be combined.")
                exit (int ExitErrorCodes.ProjectAndFscArgs)
            | Some fscArgs ->
                runFscArgs logger checker client fscArgs exclInclFiles severityMapping
                |> Async.RunSynchronously
                |> Some
            | None ->
                match projOpts, scripts with
                | [], [] ->
                    logger.LogError(
                        "No projects or scripts were specified. Use `--project` or `--script` to specify them."
                    )

                    exit (int ExitErrorCodes.EmptyFscArgs)
                | projects, scripts ->

                    for script in scripts do
                        if not (File.Exists(script)) then
                            logger.LogError(
                                "Invalid `--script` argument. File does not exist: '{script}'",
                                script
                            )

                            exit (int ExitErrorCodes.InvalidProjectArguments)

                    let scriptOptions = loadScripts logger checker scripts

                    for projPath in projects do
                        if not (File.Exists(projPath)) then
                            logger.LogError(
                                "Invalid `--project` argument. File does not exist: '{projPath}'",
                                projPath
                            )

                            exit (int ExitErrorCodes.InvalidProjectArguments)

                    async {
                        let! scriptOptions =
                            scriptOptions
                            |> Async.StartChild

                        let! loadedProjects =
                            loadProjects logger toolsPath properties projects binlogPath
                            |> Async.StartChild

                        let! loadedProjects = loadedProjects
                        let! scriptOptions = scriptOptions

                        let loadedProjects =
                            Array.toList scriptOptions
                            @ loadedProjects

                        return!
                            loadedProjects
                            |> List.map (fun (projPath: FSharpProjectOptions) ->
                                runProject
                                    logger
                                    checker
                                    client
                                    projPath
                                    exclInclFiles
                                    severityMapping
                            )
                            |> Async.Parallel
                    }
                    |> Async.RunSynchronously
                    |> List.concat
                    |> Some

    match results with
    | None -> int ExitErrorCodes.NoAnalyzersFound
    | Some results ->
        let results, hasError =
            match Result.allOkOrError results with
            | Ok results -> results, false
            | Error(results, _errors) -> results, true

        let results = results |> List.concat

        let codeRoot =
            match codeRoot with
            | None ->
                Directory.GetCurrentDirectory()
                |> Uri
            | Some root -> Path.GetFullPath root |> Uri

        match outputFormat with
        | OutputFormat.Default -> printMessagesInDefaultFormat logger results
        | OutputFormat.GitHub -> printMessagesInGitHubFormat logger codeRoot results

        report
        |> Option.iter (SarifReport.writeReport logger results codeRoot)

        let check =
            results
            |> List.exists (fun analyzerMessage ->
                let message = analyzerMessage.Message

                message.Severity = Severity.Error
            )

        if failedAssemblies > 0 then
            logger.LogError(
                "Because we failed to load some assemblies to obtain analyzers from them, exiting (failure count: {FailedAssemblyLoadCount})",
                failedAssemblies
            )

            exit (int ExitErrorCodes.FailedAssemblyLoading)

        if check then (int ExitErrorCodes.AnalyzerFoundError)
        elif hasError then (int ExitErrorCodes.AnalysisAborted)
        else (int ExitErrorCodes.Success)
