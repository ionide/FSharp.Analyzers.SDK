open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open Argu
open FSharp.Analyzers.SDK
open GlobExpressions
open Microsoft.CodeAnalysis.Sarif
open Microsoft.CodeAnalysis.Sarif.Writers
open Ionide.ProjInfo

type Arguments =
    | Project of string list
    | Analyzers_Path of string
    | Fail_On_Warnings of string list
    | Fail_On_All_Warnings of except: string list
    | Ignore_Files of string list
    | Exclude_Analyzer of string list
    | Report of string
    | Verbose

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Project _ -> "Path to your .fsproj file."
            | Analyzers_Path _ -> "Path to a folder where your analyzers are located."
            | Fail_On_Warnings _ ->
                "List of analyzer codes that should trigger tool failures in the presence of warnings."
            | Fail_On_All_Warnings _ ->
                "All analyzer codes will trigger tool failure in the presence of warnings, except for the ones listed here."
            | Ignore_Files _ -> "Source files that shouldn't be processed."
            | Exclude_Analyzer _ -> "The names of analyzers that should not be executed."
            | Report _ -> "Write the result messages to a (sarif) report file."
            | Verbose -> "Verbose logging."

type WarningConfig =
    | FailOnWarnings of string list
    | FailOnAllWarnings of except: string list

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

let printError text arg =
    Console.ForegroundColor <- ConsoleColor.Red
    printf "Error : "
    printfn text arg
    Console.ForegroundColor <- origForegroundColor

let loadProject toolsPath projPath =
    async {
        let loader = WorkspaceLoader.Create(toolsPath)
        let parsed = loader.LoadProjects [ projPath ] |> Seq.toList
        let fcsPo = FCS.mapToFSharpProjectOptions parsed.Head parsed

        return fcsPo
    }

let runProject (client: Client<CliAnalyzerAttribute, CliContext>) toolsPath proj (globs: Glob list) =
    async {
        let path = Path.Combine(Environment.CurrentDirectory, proj) |> Path.GetFullPath
        let! option = loadProject toolsPath path
        let! checkProjectResults = fcs.ParseAndCheckProject(option)

        let! messagesPerAnalyzer =
            option.SourceFiles
            |> Array.filter (fun file ->
                match globs |> List.tryFind (fun g -> g.IsMatch file) with
                | Some g ->
                    printInfo $"Ignoring file %s{file} for pattern %s{g.Pattern}"
                    false
                | None -> true
            )
            |> Array.choose (fun fileName ->
                let fileContent = File.ReadAllText fileName
                let sourceText = SourceText.ofString fileContent

                Utils.typeCheckFile
                    fcs
                    (fun s -> printError "%s" s)
                    option
                    fileName
                    (Utils.SourceOfSource.SourceText sourceText)
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
                        yield! messages
                ]
    }

let printMessages warningConfig (msgs: AnalyzerMessage list) =
    if verbose then
        printfn ""

    if verbose && List.isEmpty msgs then
        printfn "No messages found from the analyzer(s)"

    msgs
    |> Seq.iter (fun analyzerMessage ->
        let m = analyzerMessage.Message

        let color =
            match m.Severity, warningConfig with
            | Error, _ -> ConsoleColor.Red
            | Warning, FailOnWarnings inclusions when inclusions |> List.contains m.Code -> ConsoleColor.Red
            | Warning, FailOnAllWarnings exclusions when exclusions |> List.contains m.Code |> not -> ConsoleColor.Red
            | Warning, _ -> ConsoleColor.DarkYellow
            | Info, _ -> ConsoleColor.Blue
            | Hint, _ -> ConsoleColor.Cyan

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

    msgs

let writeReport (results: AnalyzerMessage list option) (report: string) =
    try
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
        let details = if not verbose then "" else $" %s{ex.Message}"
        printfn $"Could not write sarif to %s{report}%s{details}"

let calculateExitCode warningConfig (msgs: AnalyzerMessage list option) : int =
    match msgs with
    | None -> -1
    | Some msgs ->
        let check =
            msgs
            |> List.exists (fun analyzerMessage ->
                let message = analyzerMessage.Message

                match warningConfig with
                | FailOnWarnings inclusions ->
                    message.Severity = Error
                    || (message.Severity = Warning && inclusions |> List.contains message.Code)
                | FailOnAllWarnings exclusions ->
                    message.Severity = Error
                    || (message.Severity = Warning && not (exclusions |> List.contains message.Code))
            )

        if check then -2 else 0

[<EntryPoint>]
let main argv =
    let toolsPath = Init.init (DirectoryInfo Environment.CurrentDirectory) None

    let results = parser.ParseCommandLine argv
    verbose <- results.Contains <@ Verbose @>
    printInfo "Running in verbose mode"

    let warningConfig =
        match results.TryGetResult(<@ Fail_On_All_Warnings @>) with
        | Some exceptions ->
            match exceptions with
            | [] ->
                printInfo "Fail On All Warnings"
                FailOnAllWarnings []
            | _ ->
                printInfo "Fail On All Warnings Except: [%s]" (exceptions |> String.concat ", ")
                FailOnAllWarnings exceptions
        | None ->
            match results.TryGetResult(<@ Fail_On_Warnings @>) with
            | Some inclusions ->
                printInfo "Fail On Warnings: [%s]" (inclusions |> String.concat ", ")
                FailOnWarnings inclusions
            | None -> FailOnWarnings []

    let ignoreFiles = results.GetResult(<@ Ignore_Files @>, [])
    printInfo "Ignore Files: [%s]" (ignoreFiles |> String.concat ", ")
    let ignoreFiles = ignoreFiles |> List.map Glob

    let analyzersPath =
        let path = results.GetResult(<@ Analyzers_Path @>, "packages/Analyzers")

        if Path.IsPathRooted path then
            path
        else
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path))

    printInfo "Loading analyzers from %s" analyzersPath

    let excludeAnalyzers = results.GetResult(<@ Exclude_Analyzer @>, [])

    let logger =
        { new Logger with
            member _.Error msg = printError "%s" msg

            member _.Verbose msg =
                if verbose then
                    printInfo "%s" msg
        }

    let client =
        Client<CliAnalyzerAttribute, CliContext>(logger, Set.ofList excludeAnalyzers)

    let dlls, analyzers = client.LoadAnalyzers analyzersPath

    printInfo "Registered %d analyzers from %d dlls" analyzers dlls

    let projOpts = results.TryGetResult <@ Project @>
    let report = results.TryGetResult <@ Report @>

    let results =
        if analyzers = 0 then
            Some []
        else
            match projOpts with
            | None
            | Some [] ->
                printError
                    "No project given. Use `--project PATH_TO_FSPROJ`. Pass path relative to current directory.%s"
                    ""

                None
            | Some projects ->
                let runProj (proj: string) =
                    async {
                        let project =
                            if Path.IsPathRooted proj then
                                proj
                            else
                                Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, proj))

                        let! results = runProject client toolsPath project ignoreFiles
                        return results |> Option.map (printMessages warningConfig)
                    }

                projects
                |> List.map runProj
                |> Async.Sequential
                |> Async.RunSynchronously
                |> Array.choose id
                |> List.concat
                |> Some

    report |> Option.iter (writeReport results)

    calculateExitCode warningConfig results
