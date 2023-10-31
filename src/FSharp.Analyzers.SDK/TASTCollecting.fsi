namespace FSharp.Analyzers.SDK

open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Symbols.FSharpExprPatterns

module TASTCollecting =
    type TypedTreeCollectorBase =
        new: unit -> TypedTreeCollectorBase
        abstract WalkCall: range -> FSharpMemberOrFunctionOrValue -> FSharpExpr list -> unit
        default WalkCall: range -> FSharpMemberOrFunctionOrValue -> FSharpExpr list -> unit
        abstract WalkNewRecord: range -> FSharpType -> unit
        default WalkNewRecord: range -> FSharpType -> unit

    val visitExpr: handler: TypedTreeCollectorBase -> e: FSharpExpr -> unit
    val visitExprs: f: TypedTreeCollectorBase -> exprs: FSharpExpr list -> unit
    val visitObjArg: f: TypedTreeCollectorBase -> objOpt: FSharpExpr option -> unit
    val visitObjMember: f: TypedTreeCollectorBase -> memb: FSharpObjectExprOverride -> unit
    val visitDeclaration: f: TypedTreeCollectorBase -> d: FSharpImplementationFileDeclaration -> unit
    val walkTast: walker: TypedTreeCollectorBase -> decl: FSharpImplementationFileDeclaration -> unit
