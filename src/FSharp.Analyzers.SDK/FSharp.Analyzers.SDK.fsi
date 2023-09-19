namespace FSharp.Analyzers.SDK

open System
open System.Runtime.InteropServices
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text

[<AbstractClass>]
[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.Field)>]
type AnalyzerAttribute =
    new: [<Optional; DefaultParameterValue("Analyzer" :> obj)>] name: string -> AnalyzerAttribute
    inherit Attribute
    member Name: string

/// Marks an analyzer for scanning during the console application run.
type CliAnalyzerAttribute =
    new: [<Optional; DefaultParameterValue("Analyzer" :> obj)>] name: string -> CliAnalyzerAttribute
    inherit AnalyzerAttribute
    member Name: string

/// Marks an analyzer for scanning during IDE integration.
type EditorAnalyzerAttribute =
    new: [<Optional; DefaultParameterValue("Analyzer" :> obj)>] name: string -> EditorAnalyzerAttribute
    inherit AnalyzerAttribute
    member Name: string

/// Marker interface which both the CliContext and EditorContext implement
type Context =
    interface
    end

/// All the relevant compiler information for a given file.
/// Contains the source text, untyped and typed tree information.
type CliContext =
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
        /// Represents the definitional contents of a single file or fragment in an assembly, as seen by the F# language.
        /// Only available for implementation files.
        /// See <a href="https://fsharp.github.io/fsharp-compiler-docs/reference/fsharp-compiler-symbols-fsharpimplementationfilecontents.html">FSharpImplementationFileContents Type</a>
        TypedTree: FSharpImplementationFileContents option
        /// A handle to the results of the entire project
        /// See <a href="https://fsharp.github.io/fsharp-compiler-docs/reference/fsharp-compiler-codeanalysis-fsharpcheckprojectresults.html">FSharpCheckProjectResults Type</a>
        CheckProjectResults: FSharpCheckProjectResults
    }

    interface Context

    /// Collects all the types found in the CheckFileResults
    member GetAllEntities: publicOnly: bool -> AssemblySymbol list
    /// Helper for CheckProjectResults.GetAllUsesOfAllSymbols
    member GetAllSymbolUsesOfProject: unit -> FSharpSymbolUse array
    /// Helper for CheckFileResults.GetAllUsesOfAllSymbolsInFile
    member GetAllSymbolUsesOfFile: unit -> FSharpSymbolUse seq

/// Optional compiler information for a given file.
/// The available contents is controlled based on what information the IDE has available.
type EditorContext =
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
        CheckFileResults: FSharpCheckFileResults option
        /// Represents the definitional contents of a single file or fragment in an assembly, as seen by the F# language
        /// See <a href="https://fsharp.github.io/fsharp-compiler-docs/reference/fsharp-compiler-symbols-fsharpimplementationfilecontents.html">FSharpImplementationFileContents Type</a>
        TypedTree: FSharpImplementationFileContents option
        /// A handle to the results of the entire project
        /// See <a href="https://fsharp.github.io/fsharp-compiler-docs/reference/fsharp-compiler-codeanalysis-fsharpcheckprojectresults.html">FSharpCheckProjectResults Type</a>
        CheckProjectResults: FSharpCheckProjectResults option
    }

    interface Context

    /// Collects all the types found in the CheckFileResults
    member GetAllEntities: publicOnly: bool -> AssemblySymbol list
    /// Helper for CheckProjectResults.GetAllUsesOfAllSymbols
    member GetAllSymbolUsesOfProject: unit -> FSharpSymbolUse array
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

    [<RequireQualifiedAccess>]
    type SourceOfSource =
        | Path of string
        | DiscreteSource of string
        | SourceText of ISourceText

    val currentFSharpAnalyzersSDKVersion: Version

    val createFCS: documentSource: option<string -> Async<option<ISourceText>>> -> FSharpChecker

    val typeCheckFile:
        fcs: FSharpChecker ->
        printError: (string -> unit) ->
        options: FSharpProjectOptions ->
        fileName: string ->
        source: SourceOfSource ->
            option<FSharpParseFileResults * FSharpCheckFileResults>

    val createContext:
        checkProjectResults: FSharpCheckProjectResults ->
        fileName: string ->
        sourceText: ISourceText ->
        parseFileResults: FSharpParseFileResults * checkFileResults: FSharpCheckFileResults ->
            CliContext
