open System
open System.IO
open System.Runtime.Loader
open System.Runtime.InteropServices
open System.Text.RegularExpressions
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open Argu
open FSharp.Analyzers.SDK
open GlobExpressions
open Microsoft.CodeAnalysis.Sarif
open Microsoft.CodeAnalysis.Sarif.Writers
open Microsoft.Extensions.Logging
open Ionide.ProjInfo
open FSharp.Analyzers.Cli
open FSharp.Analyzers.Cli.CustomLogging


type ExitErrorCodes =
    | Success = 0
    | NoAnalyzersFound = -1
    | AnalyzerFoundError = -2
    | FailedAssemblyLoading = -3
    | AnalysisAborted = -4
    | FailedToLoadProject = 10
    | EmptyFscArgs = 11
    | MissingPropertyValue = 12
    | RuntimeAndOsOptions = 13
    | RuntimeAndArchOptions = 14
    | UnknownLoggerVerbosity = 15
    | AnalyzerListedMultipleTimesInTreatAsSeverity = 16
    | FscArgsCombinedWithMsBuildProperties = 17
    | FSharpCoreAssemblyLoadFailed = 18
    | ProjectAndFscArgs = 19
    | InvalidScriptArguments = 20
    | InvalidProjectArguments = 21
    | UnhandledException = 22

type Arguments =
    | Project of string list
    | Script of string list
    | Analyzers_Path of string list
    | [<EqualsAssignment; AltCommandLine("-p:"); AltCommandLine("-p")>] Property of string * string
    | [<Unique; AltCommandLine("-c")>] Configuration of string
    | [<Unique; AltCommandLine("-r")>] Runtime of string
    | [<Unique; AltCommandLine("-a")>] Arch of string
    | [<Unique>] Os of string
    | [<Unique>] Treat_As_Info of string list
    | [<Unique>] Treat_As_Hint of string list
    | [<Unique>] Treat_As_Warning of string list
    | [<Unique>] Treat_As_Error of string list
    | [<Unique>] Exclude_Files of string list
    | [<Unique>] Include_Files of string list
    | [<Unique>] Exclude_Analyzers of string list
    | [<Unique>] Include_Analyzers of string list
    | [<Unique>] Report of string
    | [<Unique>] FSC_Args of string
    | [<Unique>] Code_Root of string
    | [<Unique; AltCommandLine("-v")>] Verbosity of string
    | [<Unique>] Output_Format of string
    | [<Unique>] BinLog_Path of string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Project _ -> "List of paths to your .fsproj file. Cannot be combined with `--fsc-args`."
            | Script _ -> "List of paths to your .fsx file. Supports globs. Cannot be combined with `--fsc-args`."
            | Analyzers_Path _ ->
                "List of path to a folder where your analyzers are located. This will search recursively."
            | Property _ -> "A key=value pair of an MSBuild property."
            | Configuration _ -> "The configuration to use, e.g. Debug or Release."
            | Runtime _ -> "The runtime identifier (RID)."
            | Arch _ -> "The target architecture."
            | Os _ -> "The target operating system."
            | Treat_As_Info _ ->
                "List of analyzer codes that should be treated as severity Info by the tool. Regardless of the original severity."
            | Treat_As_Hint _ ->
                "List of analyzer codes that should be treated as severity Hint by the tool. Regardless of the original severity."
            | Treat_As_Warning _ ->
                "List of analyzer codes that should be treated as severity Warning by the tool. Regardless of the original severity."
            | Treat_As_Error _ ->
                "List of analyzer codes that should be treated as severity Error by the tool. Regardless of the original severity."
            | Exclude_Files _ -> "Source files that shouldn't be processed."
            | Include_Files _ ->
                "Source files that should be processed exclusively while all others are ignored. Takes precedence over --exclude-files."
            | Exclude_Analyzers _ -> "The names of analyzers that should not be executed."
            | Include_Analyzers _ ->
                "The names of analyzers that should exclusively be executed while all others are ignored. Takes precedence over --exclude-analyzers."
            | Report _ -> "Write the result messages to a (sarif) report file."
            | Verbosity _ ->
                "The verbosity level. The available verbosity levels are: n[ormal], d[etailed], diag[nostic]."
            | FSC_Args _ -> "Pass in the raw fsc compiler arguments. Cannot be combined with the `--project` flag."
            | Code_Root _ ->
                "Root of the current code repository, used in the sarif report to construct the relative file path. The current working directory is used by default."
            | Output_Format _ ->
                "Format in which to write analyzer results to stdout. The available options are: default, github."            
            | BinLog_Path(_) -> "Path to a directory where MSBuild binary logs (binlog) will be written. You can use https://msbuildlog.com/ to view them."

type SeverityMappings =
    {
        TreatAsInfo: Set<string>
        TreatAsHint: Set<string>
        TreatAsWarning: Set<string>
        TreatAsError: Set<string>
    }

    member x.IsValid() =
        let allCodes = [ x.TreatAsInfo; x.TreatAsHint; x.TreatAsWarning; x.TreatAsError ]

        let unionCount = allCodes |> Set.unionMany |> Set.count
        let summedCount = allCodes |> List.sumBy Set.count
        summedCount = unionCount

let mapMessageToSeverity (mappings: SeverityMappings) (msg: FSharp.Analyzers.SDK.AnalyzerMessage) =
    let targetSeverity =
        if mappings.TreatAsInfo |> Set.contains msg.Message.Code then
            Severity.Info
        else if mappings.TreatAsHint |> Set.contains msg.Message.Code then
            Severity.Hint
        else if mappings.TreatAsWarning |> Set.contains msg.Message.Code then
            Severity.Warning
        else if mappings.TreatAsError |> Set.contains msg.Message.Code then
            Severity.Error
        else
            msg.Message.Severity

    { msg with
        Message =
            { msg.Message with
                Severity = targetSeverity
            }
    }

[<RequireQualifiedAccess>]
type OutputFormat =
| Default
| GitHub

let parseOutputFormat = function
| "github" -> Ok OutputFormat.GitHub
| "default" -> Ok OutputFormat.Default
| other -> Error $"Unknown output format: %s{other}."

let mutable logLevel = LogLevel.Warning

let fcs = Utils.createFCS None

let parser = ArgumentParser.Create<Arguments>(errorHandler = ProcessExiter())

let rec mkKn (ty: Type) =
    if Reflection.FSharpType.IsFunction(ty) then
        let _, ran = Reflection.FSharpType.GetFunctionElements(ty)
        let f = mkKn ran
        Reflection.FSharpValue.MakeFunction(ty, (fun _ -> f))
    else
        box ()

let mutable logger: ILogger = Abstractions.NullLogger.Instance

/// <summary>Runs MSBuild to create FSharpProjectOptions based on the projPaths.</summary>
/// <returns>Returns only the FSharpProjectOptions based on the projPaths and not any referenced projects.</returns>
let loadProjects toolsPath properties (projPaths: string list) (binLogPath : DirectoryInfo option) =
    async {
        let projPaths =
            projPaths
            |> List.map (fun proj -> Path.Combine(Environment.CurrentDirectory, proj) |> Path.GetFullPath)

        for proj in projPaths do
            logger.LogInformation("Loading project {0}", proj)

        let loader = WorkspaceLoader.Create(toolsPath, properties)
        binLogPath
        |> Option.iter (fun path ->
            logger.LogInformation("Using binary log path: {0}", path.FullName)
        )
        let binLogConfig =
            binLogPath
            |> Option.map (fun path -> BinaryLogGeneration.Within path)
            |> Option.defaultValue BinaryLogGeneration.Off
            
        let projectOptions = loader.LoadProjects(projPaths, [],  binaryLog = binLogConfig)

        let failedLoads =
            projPaths
            |> Seq.filter (fun path -> not (projectOptions |> Seq.exists (fun p -> p.ProjectFileName = path)))
            |> Seq.toList

        if Seq.length failedLoads > 0 then
            logger.LogError("Failed to load project '{0}'", failedLoads)
            exit (int ExitErrorCodes.FailedToLoadProject)

        let loaded =
            FCS.mapManyOptions projectOptions
            |> Seq.filter (fun p -> projPaths |> List.exists (fun x -> x = p.ProjectFileName)) // We only want to analyze what was passed in
            |> Seq.toList

        return loaded
    }

let runProject
    (client: Client<CliAnalyzerAttribute, CliContext>)
    (fsharpOptions: FSharpProjectOptions)
    (excludeIncludeFiles: Choice<Glob list, Glob list>)
    (mappings: SeverityMappings)
    : Async<Result<AnalyzerMessage list, AnalysisFailure> list>
    =
    async {
        logger.LogInformation("Checking project {0}", fsharpOptions.ProjectFileName)
        let! checkProjectResults = fcs.ParseAndCheckProject(fsharpOptions)
        let analyzerOptions = BackgroundCompilerOptions fsharpOptions

        let! messagesPerAnalyzer =
            fsharpOptions.SourceFiles
            |> Array.filter (fun file ->
                match excludeIncludeFiles with
                | Choice1Of2 excludeFiles ->
                    match excludeFiles |> List.tryFind (fun g -> g.IsMatch file) with
                    | Some g ->
                        logger.LogInformation("Ignoring file {0} for pattern {1}", file, g.Pattern)
                        false
                    | None -> true
                | Choice2Of2 includeFiles ->
                    match includeFiles |> List.tryFind (fun g -> g.IsMatch file) with
                    | Some g ->
                        logger.LogInformation("Including file {0} for pattern {1}", file, g.Pattern)
                        true
                    | None -> false
            )
            |> Array.map (fun fileName ->
                async {
                    let! fileContent = File.ReadAllTextAsync fileName |> Async.AwaitTask
                    let sourceText = SourceText.ofString fileContent
                    logger.LogDebug("Checking file {0}", fileName)

                    // Since we did ParseAndCheckProject, we can be sure that the file is in the project.
                    // See https://fsharp.github.io/fsharp-compiler-docs/fcs/project.html for more information.
                    let! parseAndCheckResults = fcs.GetBackgroundCheckResultsForFileInProject(fileName, fsharpOptions)

                    let ctx =
                        Utils.createContext checkProjectResults fileName sourceText parseAndCheckResults analyzerOptions

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
                | Ok messages -> messages |> List.map (mapMessageToSeverity mappings) |> Ok
            )
            |> Seq.toList
    }

let fsharpFiles = set [| ".fs"; ".fsi"; ".fsx" |]

let isFSharpFile (file: string) =
    Set.exists (fun (ext: string) -> file.EndsWith(ext, StringComparison.Ordinal)) fsharpFiles

let runFscArgs
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

            if not (isFSharpFile path) || not (File.Exists path) then
                None
            else
                Some path
        )

    let otherOptions = fscArgs |> Array.filter (fun line -> not (isFSharpFile line))

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

    runProject client projectOptions excludeIncludeFiles mappings

let printMessagesInDefaultFormat (msgs: AnalyzerMessage list) =

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

let printMessagesInGitHubFormat (codeRoot : Uri) (msgs: AnalyzerMessage list) =
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

let writeReport (results: AnalyzerMessage list) (codeRoot: Uri) (report: string) =
    try
        // Construct full path to ensure path separators are normalized.
        let report = Path.GetFullPath report
        // Ensure the parent directory exists
        let reportFile = FileInfo(report)
        reportFile.Directory.Create()

        let driver = ToolComponent()
        driver.Name <- "Ionide.Analyzers.Cli"
        driver.InformationUri <- Uri("https://ionide.io/FSharp.Analyzers.SDK/")
        driver.Version <- string<Version> (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version)
        let tool = Tool()
        tool.Driver <- driver
        let run = Run()
        run.Tool <- tool

        use sarifLogger =
            new SarifLogger(
                report,
                logFilePersistenceOptions =
                    (FilePersistenceOptions.PrettyPrint ||| FilePersistenceOptions.ForceOverwrite),
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

            let physicalLocation = PhysicalLocation()

            physicalLocation.ArtifactLocation <-
                let al = ArtifactLocation()
                al.Uri <- codeRoot.MakeRelativeUri(Uri(analyzerResult.Message.Range.FileName))
                al

            physicalLocation.Region <-
                let r = Region()
                r.StartLine <- analyzerResult.Message.Range.StartLine
                r.StartColumn <- analyzerResult.Message.Range.StartColumn + 1
                r.EndLine <- analyzerResult.Message.Range.EndLine
                r.EndColumn <- analyzerResult.Message.Range.EndColumn + 1
                r

            let location: Location = Location()
            location.PhysicalLocation <- physicalLocation
            result.Locations <- [| location |]

            sarifLogger.Log(reportDescriptor, result, System.Nullable())

        sarifLogger.AnalysisStopped(RuntimeConditions.None)

        sarifLogger.Dispose()
    with ex ->
        logger.LogError(ex, "Could not write sarif to {report}", report)
        logger.LogInformation("{0}", ex)

/// If multiple MSBuild properties are given in one -p flag like -p:prop1="val1a;val1b;val1c";prop2="1;2;3";prop3=val3
/// argu will think it means prop1 has the value: "val1a;val1b;val1c";prop2="1;2;3";prop3=val3
/// so this function expands the value into multiple key-value properties
let expandMultiProperties (properties: (string * string) list) =
    properties
    |> List.map (fun (k, v) ->
        if not (v.Contains('=')) then // no multi properties given to expand
            [ (k, v) ]
        else
            let regex = Regex(";([a-z,A-Z,0-9,_,-]*)=")
            let splits = regex.Split(v)

            [
                yield (k, splits[0])

                for pair in splits.[1..] |> Seq.chunkBySize 2 do
                    match pair with
                    | [| k; v |] when String.IsNullOrWhiteSpace(v) ->
                        logger.LogError("Missing property value for '{0}'", k)
                        exit (int ExitErrorCodes.MissingPropertyValue)
                    | [| k; v |] -> yield (k, v)
                    | _ -> ()

            ]
    )
    |> List.concat

let validateRuntimeOsArchCombination (runtime, arch, os) =
    match runtime, os, arch with
    | Some _, Some _, _ ->
        logger.LogError("Specifying both the `-r|--runtime` and `-os` options is not supported.")
        exit (int ExitErrorCodes.RuntimeAndOsOptions)
    | Some _, _, Some _ ->
        logger.LogError("Specifying both the `-r|--runtime` and `-a|--arch` options is not supported.")
        exit (int ExitErrorCodes.RuntimeAndArchOptions)
    | _ -> ()

let getProperties (results: ParseResults<Arguments>) =
    let runtime = results.TryGetResult <@ Runtime @>
    let arch = results.TryGetResult <@ Arch @>
    let os = results.TryGetResult <@ Os @>
    validateRuntimeOsArchCombination (runtime, os, arch)

    let runtimeProp =
        let rid = RuntimeInformation.RuntimeIdentifier // assuming we always get something like 'linux-x64'

        match runtime, os, arch with
        | Some r, _, _ -> Some r
        | None, Some o, Some a -> Some $"{o}-{a}"
        | None, Some o, None ->
            let archOfRid = rid.Substring(rid.LastIndexOf('-') + 1)
            Some $"{o}-{archOfRid}"
        | None, None, Some a ->
            let osOfRid = rid.Substring(0, rid.LastIndexOf('-'))
            Some $"{osOfRid}-{a}"
        | _ -> None

    results.GetResults <@ Property @>
    |> expandMultiProperties
    |> fun props ->
        [
            yield! props

            match results.TryGetResult <@ Configuration @> with
            | (Some x) -> yield ("Configuration", x)
            | _ -> ()

            match runtimeProp with
            | (Some x) -> yield ("RuntimeIdentifier", x)
            | _ -> ()
        ]


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

    logger <- factory.CreateLogger("FSharp.Analyzers.Cli")

    // Set the Ionide.ProjInfo logger to use the same Microsoft.Extensions.Logging logger
    if logLevel <= LogLevel.Information then
        Ionide.ProjInfo.Logging.Providers.MicrosoftExtensionsLoggingProvider.setMicrosoftLoggerFactory factory

    logger.LogInformation("Running in verbose mode")

    let binlogPath =
        results.TryGetResult <@ BinLog_Path @>
        |> Option.map (Path.GetFullPath >> DirectoryInfo)

    AppDomain.CurrentDomain.UnhandledException.Add(fun args ->
        let ex = args.ExceptionObject :?> exn

        match ex with
        | :? FileNotFoundException as fnf when fnf.FileName.StartsWith "System.Runtime" -> 
            // https://github.com/ionide/FSharp.Analyzers.SDK/issues/245
            logger.LogCritical(ex, "FSharp.Analyzers.Cli could not find {0}. If you're using a preview version of the .NET SDK, you may need to set DOTNET_ROLL_FORWARD_TO_PRERELEASE=1 in your environment before running this tool.", fnf.FileName)
        | _ ->
            logger.LogCritical(ex, "Unhandled exception:")
        factory.Dispose() // Flush any logs https://github.com/dotnet/extensions/issues/2395
        exit (int ExitErrorCodes.UnhandledException)
    )

    let severityMapping =
        {
            TreatAsHint = results.GetResult(<@ Treat_As_Hint @>, []) |> Set.ofList
            TreatAsInfo = results.GetResult(<@ Treat_As_Info @>, []) |> Set.ofList
            TreatAsWarning = results.GetResult(<@ Treat_As_Warning @>, []) |> Set.ofList
            TreatAsError = results.GetResult(<@ Treat_As_Error @>, []) |> Set.ofList
        }

    logger.LogInformation("Treat as Hints: [{0}]", (severityMapping.TreatAsHint |> String.concat ", "))
    logger.LogInformation("Treat as Info: [{0}]", (severityMapping.TreatAsInfo |> String.concat ", "))
    logger.LogInformation("Treat as Warning: [{0}]", (severityMapping.TreatAsWarning |> String.concat ", "))
    logger.LogInformation("Treat as Error: [{0}]", (severityMapping.TreatAsError |> String.concat ", "))

    if not (severityMapping.IsValid()) then
        logger.LogError("An analyzer code may only be listed once in the <treat-as-severity> arguments.")

        exit (int ExitErrorCodes.AnalyzerListedMultipleTimesInTreatAsSeverity)

    let projOpts = results.GetResults <@ Project @> |> List.concat
    let fscArgs = results.TryGetResult <@ FSC_Args @>
    let report = results.TryGetResult <@ Report @>
    let codeRoot = results.TryGetResult <@ Code_Root @>
    let cwd = Directory.GetCurrentDirectory() |> DirectoryInfo

    let beginsWithCurrentPath (path: string) =
        path.StartsWith("./") || path.StartsWith(".\\")

    let scripts = 
        results.GetResult(<@ Script @>, [])
        |> List.collect(fun scriptGlob ->
            let root, scriptGlob = 
                if Path.IsPathRooted scriptGlob then
                    // Glob can't handle absolute paths, so we need to make sure the scriptGlob is a relative path
                    let root = Path.GetPathRoot scriptGlob
                    let glob = scriptGlob.Substring(root.Length)
                    DirectoryInfo root, glob
                else if beginsWithCurrentPath scriptGlob then
                    // Glob can't handle relative paths starting with "./" or ".\", so we need trim it
                    let relativeGlob = scriptGlob.Substring(2) // remove "./" or ".\"
                    cwd, relativeGlob
                else
                    cwd, scriptGlob

            root.GlobFiles scriptGlob |> Seq.map (fun file -> file.FullName) |> Seq.toList
        )

    let exclInclFiles =
        let excludeFiles = results.GetResult(<@ Exclude_Files @>, [])
        logger.LogInformation("Exclude Files: [{0}]", (excludeFiles |> String.concat ", "))
        let excludeFiles = excludeFiles |> List.map Glob

        let includeFiles = results.GetResult(<@ Include_Files @>, [])
        logger.LogInformation("Include Files: [{0}]", (includeFiles |> String.concat ", "))
        let includeFiles = includeFiles |> List.map Glob

        match excludeFiles, includeFiles with
        | e, [] -> Choice1Of2 e
        | [], i -> Choice2Of2 i
        | _e, i ->
            logger.LogWarning("--exclude-files and --include-files are mutually exclusive, ignoring --exclude-files")

            Choice2Of2 i

    let properties = getProperties results

    if Option.isSome fscArgs && not properties.IsEmpty then
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
            OutputFormat.Default)

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
            fun (s: string) -> e |> List.map Glob |> List.exists (fun g -> g.IsMatch s)
            |> ExcludeFilter
        | [], i ->
            fun (s: string) -> i |> List.map Glob |> List.exists (fun g -> g.IsMatch s)
            |> IncludeFilter
        | _e, i ->
            logger.LogWarning(
                "--exclude-analyzers and --include-analyzers are mutually exclusive, ignoring --exclude-analyzers"
            )

            fun (s: string) -> i |> List.map Glob |> List.exists (fun g -> g.IsMatch s)
            |> IncludeFilter

    AssemblyLoadContext.Default.add_Resolving (fun _ctx assemblyName ->
        if assemblyName.Name <> "FSharp.Core" then
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

            (accDlls + loadedDlls.AnalyzerAssemblies),
            (accAnalyzers + loadedDlls.Analyzers),
            (accFailed + loadedDlls.FailedAssemblies)
        )

    logger.LogInformation("Registered {0} analyzers from {1} dlls", analyzers, dlls)

    let results =
        if analyzers = 0 then
            None
        else
            match fscArgs with
            |  Some _ when projOpts |> List.isEmpty |> not ->
                logger.LogError("`--project` and `--fsc-args` cannot be combined.")
                exit (int ExitErrorCodes.ProjectAndFscArgs)
            |  Some _ when scripts |> List.isEmpty |> not ->
                logger.LogError("`--script` and `--fsc-args` cannot be combined.")
                exit (int ExitErrorCodes.ProjectAndFscArgs)
            | Some fscArgs ->
                runFscArgs client fscArgs exclInclFiles severityMapping
                |> Async.RunSynchronously
                |> Some
            | None ->
                match projOpts, scripts with
                | [], [] ->
                    logger.LogError("No projects or scripts were specified. Use `--project` or `--script` to specify them.")
                    exit (int ExitErrorCodes.EmptyFscArgs)
                | projects, scripts ->

                    for script in scripts do
                        if not (File.Exists(script)) then
                            logger.LogError("Invalid `--script` argument. File does not exist: '{script}'", script)
                            exit (int ExitErrorCodes.InvalidProjectArguments)

                    let scriptOptions =
                        scripts 
                        |> List.map(fun script -> async {
                            let! fileContent = File.ReadAllTextAsync script |> Async.AwaitTask
                            let sourceText = SourceText.ofString fileContent
                            // GetProjectOptionsFromScript cannot be run in parallel, it is not thread-safe.
                            let! options, diagnostics = fcs.GetProjectOptionsFromScript(script, sourceText)
                            if not (List.isEmpty diagnostics) then
                                diagnostics
                                |> List.iter (fun d ->
                                    logger.LogError(
                                        "Script {0} has a diagnostic: {1} at {2}",
                                        script,
                                        d.Message,
                                        d.Range
                                    )
                                )
                            return options
                        }
                        )
                        |> Async.Sequential

                    for projPath in projects do
                        if not (File.Exists(projPath)) then
                            logger.LogError("Invalid `--project` argument. File does not exist: '{projPath}'", projPath)
                            exit (int ExitErrorCodes.InvalidProjectArguments)
                    async {
                        let! scriptOptions = scriptOptions |> Async.StartChild
                        let! loadedProjects = loadProjects toolsPath properties projects binlogPath |> Async.StartChild
                        let! loadedProjects = loadedProjects
                        let! scriptOptions = scriptOptions

                        let loadedProjects = Array.toList scriptOptions @ loadedProjects

                        return!
                            loadedProjects
                            |> List.map (fun (projPath: FSharpProjectOptions) ->
                                runProject client projPath exclInclFiles severityMapping
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
            | None -> Directory.GetCurrentDirectory() |> Uri
            | Some root -> Path.GetFullPath root |> Uri

        match outputFormat with
        | OutputFormat.Default -> printMessagesInDefaultFormat results
        | OutputFormat.GitHub -> printMessagesInGitHubFormat codeRoot results

        report |> Option.iter (writeReport results codeRoot)

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
