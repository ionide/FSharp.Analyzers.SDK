open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text
open Argu
open FSharp.Analyzers.SDK
open GlobExpressions
open Ionide.ProjInfo

type Arguments =
    | Project of string
    | Analyzers_Path of string
    | Fail_On_Warnings of string list
    | Ignore_Files of string list
    | Verbose

    interface IArgParserTemplate with
        member s.Usage = ""

let mutable verbose = false

let fcs = Utils.createFCS None

let parser = ArgumentParser.Create<Arguments>(errorHandler = ProcessExiter())

let rec mkKn (ty: System.Type) =
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
    |> Async.RunSynchronously

let runProject toolsPath proj (globs: Glob list) =
    let path = Path.Combine(Environment.CurrentDirectory, proj) |> Path.GetFullPath
    let opts = loadProject toolsPath path

    let checkProjectResults = fcs.ParseAndCheckProject(opts) |> Async.RunSynchronously
    let allSymbolUses = checkProjectResults.GetAllUsesOfAllSymbols()

    opts.SourceFiles
    |> Array.filter (fun file ->
        match globs |> List.tryFind (fun g -> g.IsMatch file) with
        | Some g ->
            printInfo $"Ignoring file %s{file} for pattern %s{g.Pattern}"
            false
        | None -> true
    )
    |> Array.choose (fun f ->
        Utils.typeCheckFile fcs (Utils.SourceOfSource.Path f, f, opts)
        |> Option.map (Utils.createContext (checkProjectResults, allSymbolUses))
    )
    |> Array.collect (fun ctx ->
        match ctx with
        | Some c ->
            printInfo "Running analyzers for %s" c.FileName
            Client.runAnalyzers c
        | None -> failwithf "could not get context for file %s" path
    )
    |> Some

let printMessages failOnWarnings (msgs: Message array) =
    if verbose then
        printfn ""

    if verbose && Array.isEmpty msgs then
        printfn "No messages found from the analyzer(s)"

    msgs
    |> Seq.iter (fun m ->
        let color =
            match m.Severity with
            | Error -> ConsoleColor.Red
            | Warning when failOnWarnings |> List.contains m.Code -> ConsoleColor.Red
            | Warning -> ConsoleColor.DarkYellow
            | Info -> ConsoleColor.Blue

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

let calculateExitCode failOnWarnings (msgs: Message array option) : int =
    match msgs with
    | None -> -1
    | Some msgs ->
        let check =
            msgs
            |> Array.exists (fun n ->
                n.Severity = Error
                || (n.Severity = Warning && failOnWarnings |> List.contains n.Code)
            )

        if check then -2 else 0

[<EntryPoint>]
let main argv =
    let toolsPath = Init.init (IO.DirectoryInfo Environment.CurrentDirectory) None

    let results = parser.ParseCommandLine argv
    verbose <- results.Contains <@ Verbose @>
    printInfo "Running in verbose mode"

    let failOnWarnings = results.GetResult(<@ Fail_On_Warnings @>, [])
    printInfo "Fail On Warnings: [%s]" (failOnWarnings |> String.concat ", ")

    let ignoreFiles = results.GetResult(<@ Ignore_Files @>, [])
    printInfo "Ignore Files: [%s]" (ignoreFiles |> String.concat ", ")
    let ignoreFiles = ignoreFiles |> List.map Glob

    let analyzersPath =
        let path = results.GetResult(<@ Analyzers_Path @>, "packages/Analyzers")

        if System.IO.Path.IsPathRooted path then
            path
        else
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path))

    printInfo "Loading analyzers from %s" analyzersPath

    let dlls, analyzers = Client.loadAnalyzers (printError "%s") analyzersPath
    printInfo "Registered %d analyzers from %d dlls" analyzers dlls

    let projOpt = results.TryGetResult <@ Project @>

    let results =
        match projOpt with
        | None ->
            printError "No project given. Use `--project PATH_TO_FSPROJ`. Pass path relative to current directory.%s" ""
            None
        | Some proj ->
            let project =
                if System.IO.Path.IsPathRooted proj then
                    proj
                else
                    Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, proj))

            runProject toolsPath project ignoreFiles
            |> Option.map (printMessages failOnWarnings)

    calculateExitCode failOnWarnings results
