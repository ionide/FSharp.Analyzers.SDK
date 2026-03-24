namespace FSharp.Analyzers.SDK.V1

// Opaque handle to the FCS typed tree.
// This file intentionally has no .fsi so that internal members remain
// visible to later files in the assembly (Adapter.fs, TASTCollecting.fs)
// without the signature referencing any FCS types.
[<Sealed>]
type TypedTreeHandle internal (contents: FSharp.Compiler.Symbols.FSharpImplementationFileContents) =
    member internal _.Contents = contents
