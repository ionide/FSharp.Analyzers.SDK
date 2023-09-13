namespace FSharp.Analyzers.SDK

open System
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.EditorServices
open System.Runtime.InteropServices
open FSharp.Compiler.Text

module EntityCache =
    let entityCache = EntityCache()

/// Marks an analyzer for scanning
[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.Field)>]
type AnalyzerAttribute([<Optional; DefaultParameterValue "Analyzer">] name: string) =
    inherit Attribute()

    member _.Name = name

type Context =
    {
        FileName: string
        SourceText: ISourceText
        ParseFileResults: FSharpParseFileResults
        CheckFileResults: FSharpCheckFileResults
        TypedTree: FSharpImplementationFileContents
        CheckProjectResults: Async<FSharpCheckProjectResults>
    }

    member x.GetAllEntities(publicOnly: bool) =
        try
            let res =
                [
                    yield!
                        AssemblyContent.GetAssemblySignatureContent
                            AssemblyContentType.Full
                            x.CheckFileResults.PartialAssemblySignature
                    let ctx = x.CheckFileResults.ProjectContext

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
                            AssemblyContent.GetAssemblyContent
                                EntityCache.entityCache.Locking
                                contentType
                                fileName
                                signatures

                        yield! content
                ]

            res
        with _ ->
            []

    member x.GetAllSymbolUsesOfProject() =
        async {
            let! checkProjectResults = x.CheckProjectResults
            return checkProjectResults.GetAllUsesOfAllSymbols()
        }

    member x.GetAllSymbolUsesOfFile() =
        x.CheckFileResults.GetAllUsesOfAllSymbolsInFile()

type Fix =
    {
        FromRange: range
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
        Range: range
        Fixes: Fix list
    }

type Analyzer = Context -> Message list
