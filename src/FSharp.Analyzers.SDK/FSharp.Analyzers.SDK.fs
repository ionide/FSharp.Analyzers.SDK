namespace FSharp.Analyzers.SDK

#nowarn "57"

open System
open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open FSharp.Compiler.Symbols
open FSharp.Compiler.EditorServices
open System.Runtime.InteropServices
open System.Reflection

/// Marks an analyzer for scanning
[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.Field)>]
type AnalyzerAttribute([<Optional; DefaultParameterValue "Analyzer">] name: string) =
    inherit Attribute()

    member _.Name = name

type Context =
    {
        ParseFileResults: FSharpParseFileResults
        CheckFileResults: FSharpCheckFileResults
        CheckProjectResults: FSharpCheckProjectResults
        FileName: string
        Content: string[]
        TypedTree: FSharpImplementationFileContents
        GetAllEntities: bool -> AssemblySymbol list
        AllSymbolUses: FSharpSymbolUse array
        SymbolUsesOfFile: FSharpSymbolUse array
    }

type Fix =
    {
        FromRange: Text.Range
        FromText: string
        ToText: string
    }

type Severity =
    | Info
    | Warning
    | Error

type Message =
    {
        Type: string
        Message: string
        Code: string
        Severity: Severity
        Range: Text.Range
        Fixes: Fix list
    }

type Analyzer = Context -> Message list

module Utils =

    let currentFSharpAnalyzersSDKVersion =
        Assembly.GetExecutingAssembly().GetName().Version

    let private entityCache = EntityCache()

    let private getAllEntities (checkResults: FSharpCheckFileResults) (publicOnly: bool) : AssemblySymbol list =
        try
            let res =
                [
                    yield!
                        AssemblyContent.GetAssemblySignatureContent
                            AssemblyContentType.Full
                            checkResults.PartialAssemblySignature
                    let ctx = checkResults.ProjectContext

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

    let createContext
        (checkProjectResults: FSharpCheckProjectResults, allSymbolUses: FSharpSymbolUse array)
        (file, text: string, p: FSharpParseFileResults, c: FSharpCheckFileResults)
        =
        match c.ImplementationFile with
        | Some tast ->
            let context: Context =
                {
                    ParseFileResults = p
                    CheckFileResults = c
                    CheckProjectResults = checkProjectResults
                    FileName = file
                    Content = text.Split([| '\n' |])
                    TypedTree = tast
                    GetAllEntities = getAllEntities c
                    AllSymbolUses = allSymbolUses
                    SymbolUsesOfFile = allSymbolUses |> Array.filter (fun s -> s.FileName = file)
                }

            Some context
        | _ -> None

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

    let typeCheckFile (fcs: FSharpChecker) (source, file, opts) =
        let text =
            match source with
            | SourceOfSource.Path path ->
                let text = System.IO.File.ReadAllText path
                text
            | SourceOfSource.DiscreteSource s -> s

        let st = SourceText.ofString text

        let parseRes, checkAnswer =
            fcs.ParseAndCheckFileInProject(file, 0, st, opts) |> Async.RunSynchronously

        match checkAnswer with
        | FSharpCheckFileAnswer.Aborted ->
            printfn "Checking of file %s aborted" file
            None
        | FSharpCheckFileAnswer.Succeeded result -> Some(file, text, parseRes, result)
