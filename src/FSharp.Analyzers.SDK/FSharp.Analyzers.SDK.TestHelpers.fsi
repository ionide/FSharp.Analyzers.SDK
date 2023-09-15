module FSharp.Analyzers.SDK.TestHelpers

open System.Threading.Tasks
open FSharp.Compiler.CodeAnalysis

type FSharpProjectOptions with

    static member zero: FSharpProjectOptions

type Package = { Name: string; Version: string }

/// <summary>Creates a classlib project in a temporary folder to gather the needed FSharpProjectOptions.</summary>
/// <param name="framework">The target framework for the tested code to use. E.g. net6.0, net7.0</param>
/// <param name="additionalPkgs">A list of additional packages that should be referenced. The tested code can use these.</param>
/// <returns>FSharpProjectOptions</returns>
val mkOptionsFromProject: framework: string -> additionalPkgs: Package list -> Task<FSharpProjectOptions>

val getContext: opts: FSharpProjectOptions -> source: string -> Context

module AssertionHelpers =

    val areWarningsInLines: msgs: Message list -> expectedLines: Set<int> -> bool

    val messageContains: expectedContent: string -> msg: Message -> bool

    val allMessagesContain: expectedContent: string -> msgs: Message list -> bool

    val messageContainsAny: expectedContents: string list -> msg: Message -> bool

    val messagesContainAny: expectedContents: string list -> msgs: Message list -> bool
