namespace FSharp.Analyzers.SDK

open System
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.EditorServices
open System.Runtime.InteropServices
open FSharp.Compiler.Text

/// Marks an analyzer for scanning
[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.Field)>]
type AnalyzerAttribute =
    new: [<Optional; DefaultParameterValue("Analyzer" :> obj)>] name: string -> AnalyzerAttribute
    inherit Attribute
    member Name: string

/// All the relevant compiler information for a given file.
/// Contains the source text, untyped and typed tree information.
type Context =
    {
        /// The current file name.
        FileName: string
        /// Source of the current file.
        /// See <a href="https://fsharp.github.io/fsharp-compiler-docs/reference/fsharp-compiler-text-isourcetext.html">ISourceText Type</a>
        SourceText: ISourceText
        /// Represents the results of parsing an F# file and a set of analysis operations based on the parse tree alone.
        /// See <a href="https://fsharp.github.io/fsharp-compiler-docs/reference/fsharp-compiler-codeanalysis-fsharpparsefileresults.html">FSharpParseFileResults Type</a>
        ParseFileResults: FSharpParseFileResults
        /// A handle to the results of CheckFileInProject.
        /// See <a href="https://fsharp.github.io/fsharp-compiler-docs/reference/fsharp-compiler-codeanalysis-fsharpcheckfileresults.html">FSharpCheckFileResults Type</a>
        CheckFileResults: FSharpCheckFileResults
        /// Represents the definitional contents of a single file or fragment in an assembly, as seen by the F# language
        /// See <a href="https://fsharp.github.io/fsharp-compiler-docs/reference/fsharp-compiler-symbols-fsharpimplementationfilecontents.html">FSharpImplementationFileContents Type</a>
        TypedTree: FSharpImplementationFileContents
        /// A handle the results of the entire project
        /// See <a href="https://fsharp.github.io/fsharp-compiler-docs/reference/fsharp-compiler-codeanalysis-fsharpcheckprojectresults.html">FSharpCheckProjectResults Type</a>
        CheckProjectResults: Async<FSharpCheckProjectResults>
    }

    /// Collects all the types found in the CheckFileResults
    member GetAllEntities: publicOnly: bool -> AssemblySymbol list
    /// Helper for CheckProjectResults.GetAllUsesOfAllSymbols
    member GetAllSymbolUsesOfProject: unit -> Async<FSharpSymbolUse array>
    /// Helper for CheckFileResults.GetAllUsesOfAllSymbolsInFile
    member GetAllSymbolUsesOfFile: unit -> FSharpSymbolUse seq

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
