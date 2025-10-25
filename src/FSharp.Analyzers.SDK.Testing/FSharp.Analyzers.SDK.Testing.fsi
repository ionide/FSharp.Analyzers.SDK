module FSharp.Analyzers.SDK.Testing

open System.Threading.Tasks
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics

type FSharpProjectOptions with
    static member zero: FSharpProjectOptions

type ProjectSnapshot.FSharpProjectSnapshot with
    static member zero: FSharpProjectSnapshot

type Package = { Name: string; Version: string }

exception CompilerDiagnosticErrors of FSharpDiagnostic array

/// <summary>Creates a classlib project in a temporary folder to gather the needed FSharpProjectOptions.</summary>
/// <param name="framework">The target framework for the tested code to use. E.g. net6.0, net7.0</param>
/// <param name="additionalPkgs">A list of additional packages that should be referenced. The tested code can use these.</param>
/// <returns>FSharpProjectOptions</returns>
val mkOptionsFromProject:
    framework: string -> additionalPkgs: Package list -> Task<FSharpProjectOptions>

/// <summary>Creates a classlib project in a temporary folder to gather the needed FSharpProjectSnapshot.</summary>
/// <param name="framework">The target framework for the tested code to use. E.g. net6.0, net7.0</param>
/// <param name="additionalPkgs">A list of additional packages that should be referenced. The tested code can use these.</param>
/// <returns>FSharpProjectSnapshot</returns>
val mkSnapshotFromProject:
    framework: string -> additionalPkgs: list<Package> -> Task<FSharpProjectSnapshot>

type SourceFile = { FileName: string; Source: string }

/// <summary>Creates CliContext for a given set of sources and options.</summary>
/// <param name="opts">The project options to use.</param>
/// <param name="allSources">All the source files in the project.</param>
/// <param name="fileToAnalyze">The file to analyze.</param>
/// <returns>CliContext</returns>
val getContextFor:
    opts: AnalyzerProjectOptions ->
    allSources: list<SourceFile> ->
    fileToAnalyze: SourceFile ->
        Task<CliContext>

/// <summary>Creates CliContext for a given source and options.</summary>
/// <param name="opts">The project options to use.</param>
/// <param name="source">The file to analyze.</param>
/// <returns>CliContext</returns>
val getContext: opts: FSharpProjectOptions -> source: string -> CliContext

/// <summary>Creates CliContext for a given signature source and options.</summary>
/// <param name="opts">The project options to use.</param>
/// <param name="source">The file to analyze.</param>
/// <returns>CliContext</returns>
val getContextForSignature: opts: FSharpProjectOptions -> source: string -> CliContext

module Assert =

    val hasWarningsInLines: expectedLines: Set<int> -> msgs: Message list -> bool

    val messageContains: expectedContent: string -> msg: Message -> bool

    val allMessagesContain: expectedContent: string -> msgs: Message list -> bool

    val messageContainsAny: expectedContents: string list -> msg: Message -> bool

    val messagesContainAny: expectedContents: string list -> msgs: Message list -> bool
