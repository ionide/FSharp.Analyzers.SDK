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
with
    interface IArgParserTemplate with
        member s.Usage = ""

let checker =
    FSharpChecker.Create(
        projectCacheSize = 200,
        keepAllBackgroundResolutions = true,
        keepAssemblyContents = true,
        ImplicitlyStartBackgroundWork = true)

let projectSystem = ProjectController(checker)
let parser = ArgumentParser.Create<Arguments>()

let loadProject file =
    async {
        let! projLoading = projectSystem.LoadProject file ignore FSIRefs.TFM.NetCore (fun _ _ _ -> ())
        let filesToCheck =
            match projLoading with
            | ProjectResponse.Project proj ->
                proj.projectFiles
                |> List.choose (fun file ->
                    projectSystem.GetProjectOptions file
                    |> Option.map (fun opts -> file, opts)
                )
            | ProjectResponse.ProjectError(errorDetails) ->
                printfn "Project loading faield: %A" errorDetails
                []
            | ProjectResponse.ProjectLoading(_)
            | ProjectResponse.WorkspaceLoad(_) ->
                printfn "Shouldn't happen 2"
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
        printfn "Checking of file %s aborted" file
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


[<EntryPoint>]
let main argv =
    let results = parser.ParseCommandLine argv
    let projOpt = results.TryGetResult <@ Project @>

    let analyzersPath =
        Path.Combine(Environment.CurrentDirectory, results.GetResult (<@ Analyzer @>, "analyzers"))
        |> Path.GetFullPath

    let analyzers = Client.loadAnalyzers analyzersPath

    match projOpt with
    | None -> printfn "No project given. Use `--project PATH_TO_FSPROJ`. Pass path relative to current directory."
    | Some proj ->
        let path =
            Path.Combine(Environment.CurrentDirectory, proj)
            |> Path.GetFullPath

        let files =
            loadProject path
            |> List.choose typeCheckFile
            |> List.choose createContext

        let results =
            files
            |> Seq.collect (fun ctx ->
                printfn "Running analyzers for %s" ctx.FileName
                analyzers
                |> Seq.collect (fun analyzer -> analyzer ctx)
            )
            |> Seq.toList

        results
        |> Seq.iter(fun m ->
            printfn "%s(%d,%d): %s %s - %s" m.Range.FileName m.Range.StartColumn m.Range.StartLine (m.Severity.ToString()) m.Code m.Message

        )
    0 // return an integer exit code
