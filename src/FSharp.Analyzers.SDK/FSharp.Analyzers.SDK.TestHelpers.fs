module FSharp.Analyzers.SDK.TestHelpers

#nowarn "57"

open System
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text

let getAnalyzerMsgs source =
    let loadProject (fcs: FSharpChecker) fileName sourceText =
        fcs.GetProjectOptionsFromScript(fileName, sourceText)
        |> Async.RunSynchronously
        |> fst

    let fileName = "A.fs"
    let files = Map.ofArray [| (fileName, SourceText.ofString source) |]

    let documentSource fileName =
        Map.tryFind fileName files |> async.Return

    let fcs = Utils.createFCS (Some documentSource)
    let printError (s: string) = Console.WriteLine(s)
    let pathToAnalyzerDlls = System.IO.Path.GetFullPath(".")

    let foundDlls, registeredAnalyzers =
        Client.loadAnalyzers printError pathToAnalyzerDlls

    if foundDlls = 0 then
        failwith $"no Dlls found in {pathToAnalyzerDlls}"

    if registeredAnalyzers = 0 then
        failwith $"no Analyzers found in {pathToAnalyzerDlls}"

    let opts = loadProject fcs fileName files[fileName]

    let opts =
        { opts with
            SourceFiles = [| fileName |]
        }

    fcs.NotifyFileChanged(fileName, opts) |> Async.RunSynchronously // workaround for https://github.com/dotnet/fsharp/issues/15960
    let checkProjectResults = fcs.ParseAndCheckProject(opts) |> Async.RunSynchronously
    let allSymbolUses = checkProjectResults.GetAllUsesOfAllSymbols()

    if Array.isEmpty allSymbolUses then
        failwith "no symboluses"

    match Utils.typeCheckFile fcs (Utils.SourceOfSource.DiscreteSource source, fileName, opts) with
    | Some(file, text, parseRes, result) ->
        let ctx =
            Utils.createContext (checkProjectResults, allSymbolUses) (file, text, parseRes, result)

        match ctx with
        | Some c ->
            let msgs = Client.runAnalyzers c
            msgs
        | None -> failwith "Context creation failed"
    | None -> failwith "typechecking file failed"
