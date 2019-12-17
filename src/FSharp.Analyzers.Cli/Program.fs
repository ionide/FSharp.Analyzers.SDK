// Learn more about F# at http://fsharp.org

open System
open System.IO
open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Text
open ProjectSystem
open Argu
open FSharp.Analyzers.SDK

type Arguments =
    | Project of string
    | Analyzer of string
    | Fail_On_Warnings of string list
    | Verbose
with
    interface IArgParserTemplate with
        member s.Usage = ""

let mutable verbose = false

let checker =
    FSharpChecker.Create(
        projectCacheSize = 200,
        keepAllBackgroundResolutions = true,
        keepAssemblyContents = true,
        ImplicitlyStartBackgroundWork = true)

let projectSystem = ProjectController(checker)
let parser = ArgumentParser.Create<Arguments>()

let printInfo text arg =
    if verbose then
        Console.ForegroundColor <- ConsoleColor.DarkGray
        printf "Info : "
        Console.ForegroundColor <- ConsoleColor.White
        printfn text arg

let printError text arg =
    Console.ForegroundColor <- ConsoleColor.Red
    printf "Error : "
    printfn text arg
    Console.ForegroundColor <- ConsoleColor.White

let loadProject file =
    async {
        let! projLoading = projectSystem.LoadProject file ignore FSIRefs.TFM.NetCore (fun _ _ _ -> ())
        let filesToCheck =
            match projLoading with
            | ProjectResponse.Project proj ->
                printInfo "Project %s loaded" file
                proj.projectFiles
                |> List.choose (fun file ->
                    projectSystem.GetProjectOptions file
                    |> Option.map (fun opts -> file, opts)
                )
            | ProjectResponse.ProjectError(errorDetails) ->
                printError "Project loading faield: %A" errorDetails
                []
            | ProjectResponse.ProjectLoading(_)
            | ProjectResponse.WorkspaceLoad(_) ->
                []

        return filesToCheck
    } |> Async.RunSynchronously

let typeCheckFile (file,opts) =
    let text = File.ReadAllText file
    let st = SourceText.ofString text
    let (parseRes, checkAnswer) =
        checker.ParseAndCheckFileInProject(file, 1, st, opts)
        |> Async.RunSynchronously

    match checkAnswer with
    | FSharpCheckFileAnswer.Aborted ->
        printError "Checking of file %s aborted" file
        None
    | FSharpCheckFileAnswer.Succeeded(c) ->
        Some (file, text, parseRes, c)

let createContext (file, text: string, p: FSharpParseFileResults,c: FSharpCheckFileResults) =
    match p.ParseTree, c.ImplementationFile with
    | Some pt, Some tast ->
        let context : Context = {
            FileName = file
            Content = text.Split([|'\n'|])
            ParseTree = pt
            TypedTree = tast
            Symbols = c.PartialAssemblySignature.Entities |> Seq.toList
        }
        Some context
    | _ -> None

let runProject proj analyzers  =
    let path =
        Path.Combine(Environment.CurrentDirectory, proj)
        |> Path.GetFullPath

    let files =
        loadProject path
        |> List.choose typeCheckFile
        |> List.choose createContext


    files
    |> Seq.collect (fun ctx ->
        printInfo "Running analyzers for %s" ctx.FileName
        analyzers |> Seq.collect (fun analyzer -> analyzer ctx)
    )
    |> Seq.toList

let printMessages failOnWarnings (msgs: Message list) =
    if verbose then printfn ""

    msgs
    |> Seq.iter(fun m ->
        let color =
            match m.Severity with
            | Error -> ConsoleColor.Red
            | Warning when failOnWarnings |> List.contains m.Code -> ConsoleColor.Red
            | Warning -> ConsoleColor.DarkYellow
            | Info -> ConsoleColor.Blue

        Console.ForegroundColor <- color
        printfn "%s(%d,%d): %s %s - %s" m.Range.FileName m.Range.StartColumn m.Range.StartLine (m.Severity.ToString()) m.Code m.Message
        Console.ForegroundColor <- ConsoleColor.White
    )
    msgs

let calculateExitCode failOnWarnings (msgs: Message list): int =
    let check =
        msgs
        |> List.exists (fun n -> n.Severity = Error || (n.Severity = Warning && failOnWarnings |> List.contains n.Code) )

    if check then -12345 else 0

[<EntryPoint>]
let main argv =
    let results = parser.ParseCommandLine argv
    verbose <- results.Contains <@ Verbose @>
    printInfo "Running in verbose mode%s" ""

    let projOpt = results.TryGetResult <@ Project @>
    let failOnWarnings = results.GetResult(<@ Fail_On_Warnings @>, [])

    printInfo "Fail On Warnings: %A" failOnWarnings

    let analyzersPath =
        Path.Combine(Environment.CurrentDirectory, results.GetResult (<@ Analyzer @>, "analyzers"))
        |> Path.GetFullPath

    printInfo "Loading analyzers from %s" analyzersPath

    let analyzers = Client.loadAnalyzers analyzersPath
    printInfo "Registed %d analyzers" analyzers.Length

    let results =
        match projOpt with
        | None ->
            printError "No project given. Use `--project PATH_TO_FSPROJ`. Pass path relative to current directory.%s" ""
            []
        | Some proj ->
            analyzers
            |> runProject proj
            |> printMessages failOnWarnings

    calculateExitCode failOnWarnings results
