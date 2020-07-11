open System
open System.IO
open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Text
open ProjectSystem
open Argu
open FSharp.Analyzers.SDK
open GlobExpressions

type Arguments =
    | Project of string
    | Analyzers_Path of string
    | Fail_On_Warnings of string list
    | Ignore_Files of string list
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

let rec mkKn (ty: System.Type) =
    if Reflection.FSharpType.IsFunction(ty) then
        let _, ran = Reflection.FSharpType.GetFunctionElements(ty)
        let f = mkKn ran
        Reflection.FSharpValue.MakeFunction(ty, fun _ -> f)
    else
        box ()


let printInfo (fmt: Printf.TextWriterFormat<'a>) : 'a =
    if verbose then
        Console.ForegroundColor <- ConsoleColor.DarkGray
        printf "Info : "
        Console.ForegroundColor <- ConsoleColor.White
        printfn fmt
    else
        unbox (mkKn typeof<'a>)

let printError text arg =
    Console.ForegroundColor <- ConsoleColor.Red
    printf "Error : "
    printfn text arg
    Console.ForegroundColor <- ConsoleColor.White

let loadProject file =
    async {
        let! projLoading = projectSystem.LoadProject file (fun _ -> ()) FSIRefs.TFM.NetCore (fun _ _ _ -> ())
        let filesToCheck =
            match projLoading with
            | ProjectResponse.Project proj ->
                printInfo "Project %s loaded" file
                proj.projectFiles
                |> List.choose (fun file ->
                    projectSystem.GetProjectOptions file
                    |> Option.map (fun opts -> file, opts)
                )
                |> Some
            | ProjectResponse.ProjectError(errorDetails) ->
                printError "Project loading failed: %A" errorDetails
                None
            | ProjectResponse.ProjectLoading(_)
            | ProjectResponse.WorkspaceLoad(_) ->
                None

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

let entityCache = EntityCache()

let getAllEntities (checkResults: FSharpCheckFileResults) (publicOnly: bool) : AssemblySymbol list =
    try
        let res = [
          yield! AssemblyContentProvider.getAssemblySignatureContent AssemblyContentType.Full checkResults.PartialAssemblySignature
          let ctx = checkResults.ProjectContext
          let assembliesByFileName =
            ctx.GetReferencedAssemblies()
            |> Seq.groupBy (fun asm -> asm.FileName)
            |> Seq.map (fun (fileName, asms) -> fileName, List.ofSeq asms)
            |> Seq.toList
            |> List.rev // if mscorlib.dll is the first then FSC raises exception when we try to
                        // get Content.Entities from it.

          for fileName, signatures in assembliesByFileName do
            let contentType = if publicOnly then Public else Full
            let content = AssemblyContentProvider.getAssemblyContent entityCache.Locking contentType fileName signatures
            yield! content
        ]
        res
    with
    | _ -> []

let createContext (file, text: string, p: FSharpParseFileResults,c: FSharpCheckFileResults) =
    match p.ParseTree, c.ImplementationFile with
    | Some pt, Some tast ->
        let context : Context = {
            FileName = file
            Content = text.Split([|'\n'|])
            ParseTree = pt
            TypedTree = tast
            Symbols = c.PartialAssemblySignature.Entities |> Seq.toList
            GetAllEntities = getAllEntities c
        }
        Some context
    | _ -> None

let runProject proj (globs: Glob list)  =
    let path =
        Path.Combine(Environment.CurrentDirectory, proj)
        |> Path.GetFullPath

    match loadProject path with
    | None -> None
    | Some files ->

        let files =
            files
            |> List.filter (fun (f,_) ->
                match globs |> List.tryFind (fun g -> g.IsMatch f) with
                | None -> true
                | Some g ->
                    printInfo "Ignoring file %s for pattern %s" f g.Pattern
                    false)
            |> List.choose typeCheckFile
            |> List.choose createContext

        files
        |> List.collect (fun ctx ->
            printInfo "Running analyzers for %s" ctx.FileName
            Client.runAnalyzers ctx
        )
        |> Some

let printMessages failOnWarnings (msgs: Message list) =
    if verbose then printfn ""
    if verbose && List.isEmpty msgs then printfn "No messages found from the analyzer(s)"

    msgs
    |> Seq.iter(fun m ->
        let color =
            match m.Severity with
            | Error -> ConsoleColor.Red
            | Warning when failOnWarnings |> List.contains m.Code -> ConsoleColor.Red
            | Warning -> ConsoleColor.DarkYellow
            | Info -> ConsoleColor.Blue

        Console.ForegroundColor <- color
        printfn "%s(%d,%d): %s %s - %s" m.Range.FileName m.Range.StartLine m.Range.StartColumn (m.Severity.ToString()) m.Code m.Message
        Console.ForegroundColor <- ConsoleColor.White
    )
    msgs

let calculateExitCode failOnWarnings (msgs: Message list option): int =
    match msgs with
    | None -> -1
    | Some msgs ->
        let check =
            msgs
            |> List.exists (fun n -> n.Severity = Error || (n.Severity = Warning && failOnWarnings |> List.contains n.Code) )

        if check then -2 else 0

[<EntryPoint>]
let main argv =
    let results = parser.ParseCommandLine argv
    verbose <- results.Contains <@ Verbose @>
    printInfo "Running in verbose mode"

    let failOnWarnings = results.GetResult(<@ Fail_On_Warnings @>, [])
    printInfo "Fail On Warnings: [%s]" (failOnWarnings |> String.concat ", ")

    let ignoreFiles = results.GetResult(<@ Ignore_Files @>, [])
    printInfo "Ignore Files: [%s]" (ignoreFiles |> String.concat ", ")
    let ignoreFiles = ignoreFiles |> List.map Glob

    let analyzersPath =
        let path = results.GetResult (<@ Analyzers_Path @>, "packages/Analyzers")
        if System.IO.Path.IsPathRooted path
        then path
        else Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path))

    printInfo "Loading analyzers from %s" analyzersPath

    let (dlls, analyzers) = Client.loadAnalyzers analyzersPath
    printInfo "Registered %d analyzers from %d dlls" analyzers dlls

    let projOpt = results.TryGetResult <@ Project @>
    let results =
        match projOpt with
        | None ->
            printError "No project given. Use `--project PATH_TO_FSPROJ`. Pass path relative to current directory.%s" ""
            None
        | Some proj ->
            let project =
                if System.IO.Path.IsPathRooted proj
                then proj
                else Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, proj))

            runProject project ignoreFiles
            |> Option.map (printMessages failOnWarnings)

    calculateExitCode failOnWarnings results
