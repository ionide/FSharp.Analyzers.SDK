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

type Arguments =
    | Project of string list
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

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Project _ -> "Path to your .fsproj file."
            | Analyzers_Path _ -> "Path to a folder where your analyzers are located."
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

let loadProject toolsPath properties projPath =
    async {
        let loader = WorkspaceLoader.Create(toolsPath, properties)
        let parsed = loader.LoadProjects [ projPath ] |> Seq.toList

        if parsed.IsEmpty then
            logger.LogError("Failed to load project '{0}'", projPath)
            exit 1

        let fcsPo = FCS.mapToFSharpProjectOptions parsed.Head parsed

        return fcsPo
    }

let runProjectAux
    (client: Client<CliAnalyzerAttribute, CliContext>)
    (fsharpOptions: FSharpProjectOptions)
    (excludeIncludeFiles: Choice<Glob list, Glob list>)
    (mappings: SeverityMappings)
    : Async<Result<AnalyzerMessage list, AnalysisFailure> list>
    =
    async {
        let! checkProjectResults = fcs.ParseAndCheckProject(fsharpOptions)

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
                let fileContent = File.ReadAllText fileName
                let sourceText = SourceText.ofString fileContent

                Utils.typeCheckFile fcs logger fsharpOptions fileName (Utils.SourceOfSource.SourceText sourceText)
                |> Result.map (Utils.createContext checkProjectResults fileName sourceText)
            )
            |> Array.map (fun ctx ->
                match ctx with
                | Error e -> async.Return(Error e)
                | Ok ctx ->
                    async {
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

let runProject
    (client: Client<CliAnalyzerAttribute, CliContext>)
    toolsPath
    properties
    proj
    (excludeIncludeFiles: Choice<Glob list, Glob list>)
    (mappings: SeverityMappings)
    =
    async {
        let path = Path.Combine(Environment.CurrentDirectory, proj) |> Path.GetFullPath
        let! option = loadProject toolsPath properties path
        return! runProjectAux client option excludeIncludeFiles mappings
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
        exit 1
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

    runProjectAux client projectOptions excludeIncludeFiles mappings

let printMessages (msgs: AnalyzerMessage list) =

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

let writeReport (results: AnalyzerMessage list) (codeRoot: string option) (report: string) =
    try
        let codeRoot =
            match codeRoot with
            | None -> Directory.GetCurrentDirectory() |> Uri
            | Some root -> Path.GetFullPath root |> Uri

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
                        exit 1
                    | [| k; v |] -> yield (k, v)
                    | _ -> ()

            ]
    )
    |> List.concat

let validateRuntimeOsArchCombination (runtime, arch, os) =
    match runtime, os, arch with
    | Some _, Some _, _ ->
        logger.LogError("Specifying both the `-r|--runtime` and `-os` options is not supported.")
        exit 1
    | Some _, _, Some _ ->
        logger.LogError("Specifying both the `-r|--runtime` and `-a|--arch` options is not supported.")
        exit 1
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
            exit 1

    use factory =
        LoggerFactory.Create(fun builder ->
            builder
                .AddCustomFormatter(fun options -> options.UseAnalyzersMsgStyle <- false)
                .SetMinimumLevel(logLevel)
            |> ignore
        )

    logger <- factory.CreateLogger("")

    logger.LogInformation("Running in verbose mode")

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

        exit 1

    let projOpts = results.GetResults <@ Project @> |> List.concat
    let fscArgs = results.TryGetResult <@ FSC_Args @>
    let report = results.TryGetResult <@ Report @>
    let codeRoot = results.TryGetResult <@ Code_Root @>

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
        exit 1

    properties
    |> List.iter (fun (k, v) -> logger.LogInformation("Property {0}={1}", k, v))

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
        exit 1
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
            match projOpts, fscArgs with
            | [], None ->
                logger.LogError("No project given. Use `--project PATH_TO_FSPROJ`.")

                None
            | _ :: _, Some _ ->
                logger.LogError("`--project` and `--fsc-args` cannot be combined.")
                exit 1
            | [], Some fscArgs ->
                runFscArgs client fscArgs exclInclFiles severityMapping
                |> Async.RunSynchronously
                |> Some
            | projects, None ->
                for projPath in projects do
                    if not (File.Exists(projPath)) then
                        logger.LogError("Invalid `--project` argument. File does not exist: '{projPath}'", projPath)
                        exit 1

                projects
                |> List.map (fun projPath ->
                    runProject client toolsPath properties projPath exclInclFiles severityMapping
                )
                |> Async.Sequential
                |> Async.RunSynchronously
                |> List.concat
                |> Some

    match results with
    | None -> -1
    | Some results ->
        let results, hasError =
            match Result.allOkOrError results with
            | Ok results -> results, false
            | Error(results, _errors) -> results, true

        let results = results |> List.concat

        printMessages results

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

            exit -3

        if check then -2
        elif hasError then -4
        else 0
