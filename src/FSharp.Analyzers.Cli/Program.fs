open System
open System.IO
open System.Runtime.Loader
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open Argu
open FSharp.Analyzers.SDK
open GlobExpressions
open Microsoft.CodeAnalysis.Sarif
open Microsoft.CodeAnalysis.Sarif.Writers
open Ionide.ProjInfo

type Arguments =
    | [<Unique>] Project of string list
    | [<Unique>] Analyzers_Path of string list
    | [<Unique>] Fail_On_Warnings of string list
    | [<Unique>] Treat_As_Info of string list
    | [<Unique>] Treat_As_Hint of string list
    | [<Unique>] Treat_As_Warning of string list
    | [<Unique>] Treat_As_Error of string list
    | [<Unique>] Ignore_Files of string list
    | [<Unique>] Exclude_Analyzer of string list
    | [<Unique>] Report of string
    | [<Unique>] FSC_Args of string
    | [<Unique>] Verbose

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Project _ -> "Path to your .fsproj file."
            | Analyzers_Path _ -> "Path to a folder where your analyzers are located."
            | Fail_On_Warnings _ ->
                "List of analyzer codes that should trigger tool failures in the presence of warnings."
            | Treat_As_Info _ ->
                "List of analyzer codes that should be treated as severity Info by the tool. Regardless of the original severity."
            | Treat_As_Hint _ ->
                "List of analyzer codes that should be treated as severity Hint by the tool. Regardless of the original severity."
            | Treat_As_Warning _ ->
                "List of analyzer codes that should be treated as severity Warning by the tool. Regardless of the original severity."
            | Treat_As_Error _ ->
                "List of analyzer codes that should be treated as severity Error by the tool. Regardless of the original severity."
            | Ignore_Files _ -> "Source files that shouldn't be processed."
            | Exclude_Analyzer _ -> "The names of analyzers that should not be executed."
            | Report _ -> "Write the result messages to a (sarif) report file."
            | Verbose -> "Verbose logging."
            | FSC_Args _ -> "Pass in the raw fsc compiler arguments. Cannot be combined with the `--project` flag."

type SeverityMappings =
    {
        FailOnWarnings: string list
        TreatAsInfo: string list
        TreatAsHint: string list
        TreatAsWarning: string list
        TreatAsError: string list
    }

    member x.IsValid() =
        let allCodes =
            [
                x.FailOnWarnings
                x.TreatAsInfo
                x.TreatAsHint
                x.TreatAsWarning
                x.TreatAsError
            ]
            |> List.concat

        let distinctLength = allCodes |> List.distinct |> List.length
        allCodes.Length = distinctLength

let mapMessageToSeverity (mappings: SeverityMappings) (msg: FSharp.Analyzers.SDK.AnalyzerMessage) =
    let targetSeverity =
        if mappings.TreatAsInfo |> List.contains msg.Message.Code then
            Info
        else if mappings.TreatAsHint |> List.contains msg.Message.Code then
            Hint
        else if mappings.TreatAsWarning |> List.contains msg.Message.Code then
            Warning
        else if mappings.TreatAsError |> List.contains msg.Message.Code then
            Error
        else if
            mappings.FailOnWarnings |> List.contains msg.Message.Code
            && msg.Message.Severity = Warning
        then
            Error
        else
            msg.Message.Severity

    { msg with
        Message =
            { msg.Message with
                Severity = targetSeverity
            }
    }

let mutable verbose = false

let fcs = Utils.createFCS None

let parser = ArgumentParser.Create<Arguments>(errorHandler = ProcessExiter())

let rec mkKn (ty: Type) =
    if Reflection.FSharpType.IsFunction(ty) then
        let _, ran = Reflection.FSharpType.GetFunctionElements(ty)
        let f = mkKn ran
        Reflection.FSharpValue.MakeFunction(ty, (fun _ -> f))
    else
        box ()

let origForegroundColor = Console.ForegroundColor

let printInfo (fmt: Printf.TextWriterFormat<'a>) : 'a =
    if verbose then
        Console.ForegroundColor <- ConsoleColor.DarkGray
        printf "Info : "
        Console.ForegroundColor <- origForegroundColor
        printfn fmt
    else
        unbox (mkKn typeof<'a>)

let printError (text: string) : unit =
    Console.ForegroundColor <- ConsoleColor.Red
    Console.Write "Error : "
    Console.WriteLine(text)
    Console.ForegroundColor <- origForegroundColor

let loadProject toolsPath projPath =
    async {
        let loader = WorkspaceLoader.Create(toolsPath)
        let parsed = loader.LoadProjects [ projPath ] |> Seq.toList
        let fcsPo = FCS.mapToFSharpProjectOptions parsed.Head parsed

        return fcsPo
    }

let runProjectAux
    (client: Client<CliAnalyzerAttribute, CliContext>)
    (fsharpOptions: FSharpProjectOptions)
    (ignoreFiles: Glob list)
    (mappings: SeverityMappings)
    =
    async {
        let! checkProjectResults = fcs.ParseAndCheckProject(fsharpOptions)

        let! messagesPerAnalyzer =
            fsharpOptions.SourceFiles
            |> Array.filter (fun file ->
                match ignoreFiles |> List.tryFind (fun g -> g.IsMatch file) with
                | Some g ->
                    printInfo $"Ignoring file %s{file} for pattern %s{g.Pattern}"
                    false
                | None -> true
            )
            |> Array.choose (fun fileName ->
                let fileContent = File.ReadAllText fileName
                let sourceText = SourceText.ofString fileContent

                Utils.typeCheckFile fcs printError fsharpOptions fileName (Utils.SourceOfSource.SourceText sourceText)
                |> Option.map (Utils.createContext checkProjectResults fileName sourceText)
            )
            |> Array.map (fun ctx ->
                printInfo "Running analyzers for %s" ctx.FileName
                client.RunAnalyzers ctx
            )
            |> Async.Parallel

        return
            Some
                [
                    for messages in messagesPerAnalyzer do
                        let mappedMessages = messages |> List.map (mapMessageToSeverity mappings)
                        yield! mappedMessages
                ]
    }

let runProject
    (client: Client<CliAnalyzerAttribute, CliContext>)
    toolsPath
    proj
    (globs: Glob list)
    (mappings: SeverityMappings)
    =
    async {
        let path = Path.Combine(Environment.CurrentDirectory, proj) |> Path.GetFullPath
        let! option = loadProject toolsPath path
        return! runProjectAux client option globs mappings
    }

let fsharpFiles = set [| ".fs"; ".fsi"; ".fsx" |]

let isFSharpFile (file: string) =
    Seq.exists (fun (ext: string) -> file.EndsWith ext) fsharpFiles

let runFscArgs
    (client: Client<CliAnalyzerAttribute, CliContext>)
    (fscArgs: string)
    (globs: Glob list)
    (mappings: SeverityMappings)
    =
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

    runProjectAux client projectOptions globs mappings

let printMessages (msgs: AnalyzerMessage list) =
    if verbose then
        printfn ""

    if verbose && List.isEmpty msgs then
        printfn "No messages found from the analyzer(s)"

    msgs
    |> Seq.iter (fun analyzerMessage ->
        let m = analyzerMessage.Message

        let color =
            match m.Severity with
            | Error -> ConsoleColor.Red
            | Warning -> ConsoleColor.DarkYellow
            | Info -> ConsoleColor.Blue
            | Hint -> ConsoleColor.Cyan

        Console.ForegroundColor <- color

        printfn
            "%s(%d,%d): %s %s - %s"
            m.Range.FileName
            m.Range.StartLine
            m.Range.StartColumn
            (m.Severity.ToString())
            m.Code
            m.Message

        Console.ForegroundColor <- origForegroundColor
    )

    ()

let writeReport (results: AnalyzerMessage list option) (report: string) =
    try
        // Construct full path to ensure path separators are normalized.
        let report = Path.GetFullPath report
        // Ensure the parent directory exists
        let reportFile = FileInfo(report)
        reportFile.Directory.Create()

        let driver = ToolComponent()
        driver.Name <- "Ionide.Analyzers.Cli"
        driver.InformationUri <- Uri("https://ionide.io/FSharp.Analyzers.SDK/")
        driver.Version <- string (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version)
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

        for analyzerResult in (Option.defaultValue List.empty results) do
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
                | Info -> FailureLevel.Note
                | Hint -> FailureLevel.None
                | Warning -> FailureLevel.Warning
                | Error -> FailureLevel.Error

            let msg = Message()
            msg.Text <- analyzerResult.Message.Message
            result.Message <- msg

            let physicalLocation = PhysicalLocation()

            physicalLocation.ArtifactLocation <-
                let al = ArtifactLocation()
                al.Uri <- Uri(analyzerResult.Message.Range.FileName)
                al

            physicalLocation.Region <-
                let r = Region()
                r.StartLine <- analyzerResult.Message.Range.StartLine
                r.StartColumn <- analyzerResult.Message.Range.StartColumn
                r.EndLine <- analyzerResult.Message.Range.EndLine
                r.EndColumn <- analyzerResult.Message.Range.EndColumn
                r

            let location: Location = Location()
            location.PhysicalLocation <- physicalLocation
            result.Locations <- [| location |]

            sarifLogger.Log(reportDescriptor, result, System.Nullable())

        sarifLogger.AnalysisStopped(RuntimeConditions.None)

        sarifLogger.Dispose()
    with ex ->
        let details = if not verbose then "" else $" %A{ex}"
        printfn $"Could not write sarif to %s{report}%s{details}"

let calculateExitCode (msgs: AnalyzerMessage list option) : int =
    match msgs with
    | None -> -1
    | Some msgs ->
        let check =
            msgs
            |> List.exists (fun analyzerMessage ->
                let message = analyzerMessage.Message

                message.Severity = Error
            )

        if check then -2 else 0

[<EntryPoint>]
let main argv =
    let toolsPath = Init.init (DirectoryInfo Environment.CurrentDirectory) None

    let results = parser.ParseCommandLine argv
    verbose <- results.Contains <@ Verbose @>
    printInfo "Running in verbose mode"

    let severityMapping =
        {
            FailOnWarnings = results.GetResult(<@ Fail_On_Warnings @>, [])
            TreatAsHint = results.GetResult(<@ Treat_As_Hint @>, [])
            TreatAsInfo = results.GetResult(<@ Treat_As_Info @>, [])
            TreatAsWarning = results.GetResult(<@ Treat_As_Warning @>, [])
            TreatAsError = results.GetResult(<@ Treat_As_Error @>, [])
        }

    printInfo "Fail On Warnings: [%s]" (severityMapping.FailOnWarnings |> String.concat ", ")
    printInfo "Treat as Hints: [%s]" (severityMapping.TreatAsHint |> String.concat ", ")
    printInfo "Treat as Info: [%s]" (severityMapping.TreatAsInfo |> String.concat ", ")
    printInfo "Treat as Warning: [%s]" (severityMapping.TreatAsWarning |> String.concat ", ")
    printInfo "Treat as Error: [%s]" (severityMapping.TreatAsError |> String.concat ", ")

    if not (severityMapping.IsValid()) then
        printError
            "An analyzer code may only be listed once in the <fail-on-warnings> and <treat-as-severity> arguments."

        exit 1

    let ignoreFiles = results.GetResult(<@ Ignore_Files @>, [])
    printInfo "Ignore Files: [%s]" (ignoreFiles |> String.concat ", ")
    let ignoreFiles = ignoreFiles |> List.map Glob

    let analyzersPaths =
        results.GetResult(<@ Analyzers_Path @>, [ "packages/Analyzers" ])
        |> List.map (fun path ->
            if Path.IsPathRooted path then
                path
            else
                Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path))
        )

    printInfo "Loading analyzers from %s" (String.concat ", " analyzersPaths)

    let excludeAnalyzers = results.GetResult(<@ Exclude_Analyzer @>, [])

    let logger =
        { new Logger with
            member _.Error msg = printError msg

            member _.Verbose msg =
                if verbose then
                    printInfo "%s" msg
        }

    AssemblyLoadContext.Default.add_Resolving (fun _ctx assemblyName ->
        if assemblyName.Name <> "FSharp.Core" then
            null
        else

        let msg =
            $"""Could not load FSharp.Core %A{assemblyName.Version}. The expected assembly version of FSharp.Core is %A{Utils.currentFSharpCoreVersion}.
        Consider adding <PackageReference Update="FSharp.Core" Version="<CorrectVersion>" /> to your .fsproj.
        The correct version can be found over at https://www.nuget.org/packages/FSharp.Analyzers.SDK#dependencies-body-tab.
        """

        printError msg
        exit 1
    )

    let client =
        Client<CliAnalyzerAttribute, CliContext>(logger, Set.ofList excludeAnalyzers)

    let dlls, analyzers =
        ((0, 0), analyzersPaths)
        ||> List.fold (fun (accDlls, accAnalyzers) analyzersPath ->
            let dlls, analyzers = client.LoadAnalyzers analyzersPath
            (accDlls + dlls), (accAnalyzers + analyzers)
        )

    printInfo "Registered %d analyzers from %d dlls" analyzers dlls

    let projOpts = results.TryGetResult <@ Project @>
    let fscArgs = results.TryGetResult <@ FSC_Args @>
    let report = results.TryGetResult <@ Report @>

    let results =
        if analyzers = 0 then
            Some []
        else
            match projOpts, fscArgs with
            | None, None
            | Some [], None ->
                printError
                    "No project given. Use `--project PATH_TO_FSPROJ`. Pass path relative to current directory.%s"

                None
            | Some _, Some _ ->
                printError "`--project` and `--fsc-args` cannot be combined."
                exit 1
            | None, Some fscArgs -> runFscArgs client fscArgs ignoreFiles severityMapping |> Async.RunSynchronously
            | Some projects, None ->
                let runProj (proj: string) =
                    async {
                        let project =
                            if Path.IsPathRooted proj then
                                proj
                            else
                                Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, proj))

                        let! results = runProject client toolsPath project ignoreFiles severityMapping
                        return results
                    }

                projects
                |> List.map runProj
                |> Async.Sequential
                |> Async.RunSynchronously
                |> Array.choose id
                |> List.concat
                |> Some

    results |> Option.iter printMessages
    report |> Option.iter (writeReport results)

    calculateExitCode results
