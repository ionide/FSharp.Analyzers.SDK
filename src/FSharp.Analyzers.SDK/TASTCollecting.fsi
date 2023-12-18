namespace FSharp.Analyzers.SDK

open Microsoft.Extensions.Logging
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

module TASTCollecting =

    /// The members of this type are called by walkTast.
    /// By overwriting the members for various tree elements, a custom operation can be executed for them.
    type TypedTreeCollectorBase =
        new: unit -> TypedTreeCollectorBase

        /// Overwriting this member hooks up a custom operation for a call of a member or function.
        abstract WalkCall:
            objExprOpt: FSharpExpr option ->
            memberOrFunc: FSharpMemberOrFunctionOrValue ->
            objExprTypeArgs: FSharpType list ->
            memberOrFuncTypeArgs: FSharpType list ->
            argExprs: FSharpExpr list ->
            exprRange: range ->
                unit

        default WalkCall:
            objExprOpt: FSharpExpr option ->
            memberOrFunc: FSharpMemberOrFunctionOrValue ->
            objTypeArgs: FSharpType list ->
            memberOrFuncTypeArgs: FSharpType list ->
            argExprs: FSharpExpr list ->
            exprRange: range ->
                unit

        /// Overwriting this member hooks up a custom operation for the creation of a new record instance.
        abstract WalkNewRecord: recordType: FSharpType -> argExprs: FSharpExpr list -> exprRange: range -> unit
        default WalkNewRecord: recordType: FSharpType -> argExprs: FSharpExpr list -> exprRange: range -> unit

    /// Traverses the whole TAST and calls the appropriate members of the given TypedTreeCollectorBase
    /// to process the tree elements.
    val walkTast: walker: TypedTreeCollectorBase -> tast: FSharpImplementationFileContents -> unit

    /// Set this to use a custom logger
    val mutable logger: ILogger
