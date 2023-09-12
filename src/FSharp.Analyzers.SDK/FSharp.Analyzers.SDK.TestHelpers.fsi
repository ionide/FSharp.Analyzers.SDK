module FSharp.Analyzers.SDK.TestHelpers

open FSharp.Compiler.CodeAnalysis

type DotNetVersion =
    | Six
    | Seven

    override ToString: unit -> string

type FSharpProjectOptions with

    static member zero: FSharpProjectOptions

type Package = { Name: string; Version: string }

val mkOptionsFromProject: version: DotNetVersion -> additionalPkg: Package option -> FSharpProjectOptions

val getContext: opts: FSharpProjectOptions -> source: string -> Context
