namespace FSharp.Analyzers.SDK

open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

module TASTCollecting =
    type TypedTreeCollectorBase =
        new: unit -> TypedTreeCollectorBase
        abstract WalkCall: range -> FSharpMemberOrFunctionOrValue -> FSharpExpr list -> unit
        default WalkCall: range -> FSharpMemberOrFunctionOrValue -> FSharpExpr list -> unit
        abstract WalkNewRecord: range -> FSharpType -> unit
        default WalkNewRecord: range -> FSharpType -> unit

    val walkTast: walker: TypedTreeCollectorBase -> decl: FSharpImplementationFileDeclaration -> unit
