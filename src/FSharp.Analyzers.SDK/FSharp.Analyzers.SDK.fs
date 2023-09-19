namespace FSharp.Analyzers.SDK

#nowarn "57"

open System
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.EditorServices
open System.Reflection
open System.Runtime.InteropServices
open FSharp.Compiler.Text

module EntityCache =
    let private entityCache = EntityCache()

    let getEntities (publicOnly: bool) (checkFileResults: FSharpCheckFileResults) =
        try
            let res =
                [
                    yield!
                        AssemblyContent.GetAssemblySignatureContent
                            AssemblyContentType.Full
                            checkFileResults.PartialAssemblySignature
                    let ctx = checkFileResults.ProjectContext

                    let assembliesByFileName =
                        ctx.GetReferencedAssemblies()
                        |> Seq.groupBy (fun asm -> asm.FileName)
                        |> Seq.map (fun (fileName, asms) -> fileName, List.ofSeq asms)
                        |> Seq.toList
                        |> List.rev // if mscorlib.dll is the first then FSC raises exception when we try to
                    // get Content.Entities from it.

                    for fileName, signatures in assembliesByFileName do
                        let contentType =
                            if publicOnly then
                                AssemblyContentType.Public
                            else
                                AssemblyContentType.Full

                        let content =
                            AssemblyContent.GetAssemblyContent entityCache.Locking contentType fileName signatures

                        yield! content
                ]

            res
        with _ ->
            []

[<AbstractClass>]
[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.Field)>]
type AnalyzerAttribute([<Optional; DefaultParameterValue("Analyzer" :> obj)>] name: string) =
    inherit Attribute()
    member val Name: string = name

[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.Field)>]
type CliAnalyzerAttribute([<Optional; DefaultParameterValue "Analyzer">] name: string) =
    inherit AnalyzerAttribute(name)

    member _.Name = name

[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.Field)>]
type EditorAnalyzerAttribute([<Optional; DefaultParameterValue "Analyzer">] name: string) =
    inherit AnalyzerAttribute(name)

    member _.Name = name

type Context =
    interface
    end

type CliContext =
    {
        FileName: string
        SourceText: ISourceText
        ParseFileResults: FSharpParseFileResults
        CheckFileResults: FSharpCheckFileResults
        TypedTree: FSharpImplementationFileContents option
        CheckProjectResults: FSharpCheckProjectResults
    }

    interface Context

    member x.GetAllEntities(publicOnly: bool) =
        EntityCache.getEntities publicOnly x.CheckFileResults

    member x.GetAllSymbolUsesOfProject() =
        x.CheckProjectResults.GetAllUsesOfAllSymbols()

    member x.GetAllSymbolUsesOfFile() =
        x.CheckFileResults.GetAllUsesOfAllSymbolsInFile()

type EditorContext =
    {
        FileName: string
        SourceText: ISourceText
        ParseFileResults: FSharpParseFileResults
        CheckFileResults: FSharpCheckFileResults option
        TypedTree: FSharpImplementationFileContents option
        CheckProjectResults: FSharpCheckProjectResults option
    }

    interface Context

    member x.GetAllEntities(publicOnly: bool) : AssemblySymbol list =
        match x.CheckFileResults with
        | None -> List.empty
        | Some checkFileResults -> EntityCache.getEntities publicOnly checkFileResults

    member x.GetAllSymbolUsesOfProject() : FSharpSymbolUse array =
        match x.CheckProjectResults with
        | None -> Array.empty
        | Some checkProjectResults -> checkProjectResults.GetAllUsesOfAllSymbols()

    member x.GetAllSymbolUsesOfFile() : FSharpSymbolUse seq =
        match x.CheckFileResults with
        | None -> Seq.empty
        | Some checkFileResults -> checkFileResults.GetAllUsesOfAllSymbolsInFile()

type Fix =
    {
        FromRange: range
        FromText: string
        ToText: string
    }

type Severity =
    | Info
    | Hint
    | Warning
    | Error

type Message =
    {
        Type: string
        Message: string
        Code: string
        Severity: Severity
        Range: range
        Fixes: Fix list
    }

type Analyzer<'TContext> = 'TContext -> Async<Message list>

module Utils =

    let currentFSharpAnalyzersSDKVersion =
        Assembly.GetExecutingAssembly().GetName().Version

    let createContext
        (checkProjectResults: FSharpCheckProjectResults)
        (fileName: string)
        (sourceText: ISourceText)
        ((parseFileResults: FSharpParseFileResults, checkFileResults: FSharpCheckFileResults))
        : CliContext
        =
        {
            FileName = fileName
            SourceText = sourceText
            ParseFileResults = parseFileResults
            CheckFileResults = checkFileResults
            TypedTree = checkFileResults.ImplementationFile
            CheckProjectResults = checkProjectResults
        }

    let createFCS documentSource =
        let ds =
            documentSource
            |> Option.map DocumentSource.Custom
            |> Option.defaultValue DocumentSource.FileSystem

        FSharpChecker.Create(
            projectCacheSize = 200,
            keepAllBackgroundResolutions = true,
            keepAssemblyContents = true,
            documentSource = ds
        )

    [<RequireQualifiedAccess>]
    type SourceOfSource =
        | Path of string
        | DiscreteSource of string
        | SourceText of ISourceText

    let typeCheckFile
        (fcs: FSharpChecker)
        (printError: (string -> unit))
        (options: FSharpProjectOptions)
        (fileName: string)
        (source: SourceOfSource)
        =

        let sourceText =
            match source with
            | SourceOfSource.Path path ->
                let text = System.IO.File.ReadAllText path
                SourceText.ofString text
            | SourceOfSource.DiscreteSource s -> SourceText.ofString s
            | SourceOfSource.SourceText s -> s

        let parseRes, checkAnswer =
            fcs.ParseAndCheckFileInProject(fileName, 0, sourceText, options)
            |> Async.RunSynchronously //ToDo: Validate if 0 is ok

        match checkAnswer with
        | FSharpCheckFileAnswer.Aborted ->
            printError $"Checking of file {fileName} aborted"
            None
        | FSharpCheckFileAnswer.Succeeded result -> Some(parseRes, result)
