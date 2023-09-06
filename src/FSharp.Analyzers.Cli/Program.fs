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
with
    interface IArgParserTemplate with
        member s.Usage = ""

let mutable verbose = false


let createFCS () =
    let checker = FSharpChecker.Create(projectCacheSize = 200, keepAllBackgroundResolutions = true, keepAssemblyContents = true)
    // checker.ImplicitlyStartBackgroundWork <- true
    checker
let fcs = createFCS ()

let parser = ArgumentParser.Create<Arguments>(errorHandler = ProcessExiter ())

let rec mkKn (ty: System.Type) =
    if Reflection.FSharpType.IsFunction(ty) then
        let _, ran = Reflection.FSharpType.GetFunctionElements(ty)
        let f = mkKn ran
        Reflection.FSharpValue.MakeFunction(ty, fun _ -> f)
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
    } |> Async.RunSynchronously

let typeCheckFile (file,opts) =
    let text = File.ReadAllText file
    let st = SourceText.ofString text
    let (parseRes, checkAnswer) = fcs.ParseAndCheckFileInProject(file,0,st,opts) |> Async.RunSynchronously //ToDo: Validate if 0 is ok
    match checkAnswer with
    | FSharpCheckFileAnswer.Aborted ->
        printError "Checking of file %s aborted" file
        None
    | FSharpCheckFileAnswer.Succeeded result ->
        Some (file, text, parseRes, result)

let entityCache = EntityCache()

let getAllEntities (checkResults: FSharpCheckFileResults) (publicOnly: bool) : AssemblySymbol list =
    try
        let res = [
          yield! AssemblyContent.GetAssemblySignatureContent AssemblyContentType.Full checkResults.PartialAssemblySignature
          let ctx = checkResults.ProjectContext
          let assembliesByFileName =
            ctx.GetReferencedAssemblies()
            |> Seq.groupBy (fun asm -> asm.FileName)
            |> Seq.map (fun (fileName, asms) -> fileName, List.ofSeq asms)
            |> Seq.toList
            |> List.rev // if mscorlib.dll is the first then FSC raises exception when we try to
                        // get Content.Entities from it.

          for fileName, signatures in assembliesByFileName do
            let contentType = if publicOnly then AssemblyContentType.Public else AssemblyContentType.Full
            let content = AssemblyContent.GetAssemblyContent entityCache.Locking contentType fileName signatures
            yield! content
        ]
        res
    with
    | _ -> []

let createContext
    (checkProjectResults: FSharpCheckProjectResults, allSymbolUses: FSharpSymbolUse array)
    (file, text: string, p: FSharpParseFileResults, c: FSharpCheckFileResults) =
    match c.ImplementationFile with
    | Some tast ->
        let context : Context = {
            ParseFileResults = p
            CheckFileResults = c
            CheckProjectResults = checkProjectResults 
            FileName = file
            Content = text.Split([|'\n'|])
            TypedTree = tast
            GetAllEntities = getAllEntities c
            AllSymbolUses = allSymbolUses
            SymbolUsesOfFile = allSymbolUses |> Array.filter (fun s -> s.FileName = file) 
        }
        Some context
    | _ -> None


let runProject toolsPath proj (globs: Glob list)  =
    let path =
        Path.Combine(Environment.CurrentDirectory, proj)
        |> Path.GetFullPath
    let opts = loadProject toolsPath path
    
    let checkProjectResults = fcs.ParseAndCheckProject(opts) |> Async.RunSynchronously
    let allSymbolUses = checkProjectResults.GetAllUsesOfAllSymbols()
    
    opts.SourceFiles
    |> Array.filter (fun file ->
        match Path.GetExtension(file).ToLowerInvariant() with
        | ".fsi" ->
            printInfo $"Ignoring signature file %s{file}"
            false
        | _ -> true
    )
    |> Array.filter (fun file ->
        match globs |> List.tryFind (fun g -> g.IsMatch file) with
        | Some g ->
            printInfo $"Ignoring file %s{file} for pattern %s{g.Pattern}"
            false
        | None -> true
    )
    |> Array.choose (fun f -> typeCheckFile (f, opts) |> Option.map (createContext (checkProjectResults, allSymbolUses)))
    |> Array.collect (fun ctx ->
        match ctx with
        | Some c ->
            printInfo "Running analyzers for %s" c.FileName
            Client.runAnalyzers c
        | None -> failwithf "could not get context for file %s" path
            )
        |> Some

let printMessages failOnWarnings (msgs: Message array) =
    if verbose then printfn ""
    if verbose && Array.isEmpty msgs then printfn "No messages found from the analyzer(s)"

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
        Console.ForegroundColor <- origForegroundColor
    )
    msgs

let calculateExitCode failOnWarnings (msgs: Message array option): int =
    match msgs with
    | None -> -1
    | Some msgs ->
        let check =
            msgs
            |> Array.exists (fun n -> n.Severity = Error || (n.Severity = Warning && failOnWarnings |> List.contains n.Code) )

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
        let path = results.GetResult (<@ Analyzers_Path @>, "packages/Analyzers")
        if System.IO.Path.IsPathRooted path
        then path
        else Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path))

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
                if System.IO.Path.IsPathRooted proj
                then proj
                else Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, proj))

            runProject toolsPath project ignoreFiles
            |> Option.map (printMessages failOnWarnings)

    calculateExitCode failOnWarnings results
