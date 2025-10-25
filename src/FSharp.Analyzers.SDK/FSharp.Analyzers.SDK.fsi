namespace FSharp.Analyzers.SDK

open System
open System.Runtime.InteropServices
open Microsoft.Extensions.Logging
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text

type AnalyzerIgnoreRange =
    | File
    | Range of commentStart: int * commentEnd: int
    | NextLine of commentLine: int
    | CurrentLine of commentLine: int

module Ignore =
    val getAnalyzerIgnoreRanges:
        parseFileResults: FSharpParseFileResults ->
        sourceText: ISourceText ->
            Map<string, AnalyzerIgnoreRange list>

[<AbstractClass>]
[<AttributeUsage(AttributeTargets.Method
                 ||| AttributeTargets.Property
                 ||| AttributeTargets.Field)>]
type AnalyzerAttribute =
    new:
        [<Optional; DefaultParameterValue("Analyzer" :> obj)>] name: string *
        [<Optional; DefaultParameterValue("" :> obj)>] shortDescription: string *
        [<Optional; DefaultParameterValue("" :> obj)>] helpUri: string ->
            AnalyzerAttribute

    inherit Attribute
    member Name: string
    member ShortDescription: string option
    member HelpUri: string option

/// Marks an analyzer for scanning during the console application run.
type CliAnalyzerAttribute =
    new:
        [<Optional; DefaultParameterValue("Analyzer" :> obj)>] name: string *
        [<Optional; DefaultParameterValue("" :> obj)>] shortDescription: string *
        [<Optional; DefaultParameterValue("" :> obj)>] helpUri: string ->
            CliAnalyzerAttribute

    inherit AnalyzerAttribute
    member Name: string

/// Marks an analyzer for scanning during IDE integration.
type EditorAnalyzerAttribute =
    new:
        [<Optional; DefaultParameterValue("Analyzer" :> obj)>] name: string *
        [<Optional; DefaultParameterValue("" :> obj)>] shortDescription: string *
        [<Optional; DefaultParameterValue("" :> obj)>] helpUri: string ->
            EditorAnalyzerAttribute

    inherit AnalyzerAttribute
    member Name: string

/// Marker interface which both the CliContext and EditorContext implement
type Context =
    abstract member AnalyzerIgnoreRanges: Map<string, AnalyzerIgnoreRange list>

/// Options related to the project being analyzed.
type AnalyzerProjectOptions =
    | BackgroundCompilerOptions of FSharpProjectOptions
    | TransparentCompilerOptions of FSharpProjectSnapshot

    /// The current project name.
    member ProjectFileName: string
    /// The identifier of the current project.
    member ProjectId: string option
    /// The set of source files in the current project.
    member SourceFiles: string list
    /// Projects referenced by this project.
    member ReferencedProjectsPath: string list
    /// The time at which the project was loaded.
    member LoadTime: DateTime
    /// Additional command line argument options for the project.
    member OtherOptions: string list

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
        /// Options related to the project being analyzed.
        ProjectOptions: AnalyzerProjectOptions
        /// Ranges in the file to ignore for specific analyzers codes
        AnalyzerIgnoreRanges: Map<string, AnalyzerIgnoreRange list>
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
        // Options related to the project being analyzed.
        ProjectOptions: AnalyzerProjectOptions
        /// Ranges in the file to ignore for specific analyzers codes
        AnalyzerIgnoreRanges: Map<string, AnalyzerIgnoreRange list>
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

[<RequireQualifiedAccess>]
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

type AnalyzerMessage =
    {
        /// A message produced by the analyzer.
        Message: Message
        /// Either the Name property used from the AnalyzerAttribute of the name or the function or member.
        Name: string
        /// Assembly the analyzer was found in.
        AssemblyPath: string
        /// Short description for the analyzer. Used in the sarif output.
        ShortDescription: string option
        /// A link to the documentation of this analyzer. Used in the sarif output.
        HelpUri: string option
    }

/// Represents a failure to run FSC analysis.
[<RequireQualifiedAccess>]
type AnalysisFailure =
    /// The F# compiler service aborted during analysis.
    | Aborted

module Utils =

    [<RequireQualifiedAccess>]
    type SourceOfSource =
        | Path of string
        | DiscreteSource of string
        | SourceText of ISourceText

    val currentFSharpAnalyzersSDKVersion: Version
    val currentFSharpCoreVersion: Version

    val createFCS: documentSource: option<string -> Async<option<ISourceText>>> -> FSharpChecker

    val typeCheckFile:
        fcs: FSharpChecker ->
        logger: ILogger ->
        options: AnalyzerProjectOptions ->
        fileName: string ->
        source: SourceOfSource ->
            Async<Result<(FSharpParseFileResults * FSharpCheckFileResults), AnalysisFailure>>

    val createContext:
        checkProjectResults: FSharpCheckProjectResults ->
        fileName: string ->
        sourceText: ISourceText ->
        parseFileResults: FSharpParseFileResults * checkFileResults: FSharpCheckFileResults ->
            projectOptions: AnalyzerProjectOptions ->
                CliContext
