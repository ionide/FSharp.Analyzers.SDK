namespace FSharp.Analyzers.SDK

open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

module TASTCollecting =

    /// The members of this type are called by walkTast.
    /// By overwriting the members for various tree elements, a custom operation can be executed for them.
    type TypedTreeCollectorBase =
        new: unit -> TypedTreeCollectorBase

        /// Overwriting this member hooks up a custom operation for a call of a member or function.
        abstract WalkCall: range -> FSharpMemberOrFunctionOrValue -> FSharpExpr list -> unit
        default WalkCall: range -> FSharpMemberOrFunctionOrValue -> FSharpExpr list -> unit

        /// Overwriting this member hooks up a custom operation for the creation of a new record instance.
        abstract WalkNewRecord: range -> FSharpType -> unit
        default WalkNewRecord: range -> FSharpType -> unit

    /// Traverses the whole TAST and calls the appropriate members of the given TypedTreeCollectorBase
    /// to process the tree elements.
    val walkTast: walker: TypedTreeCollectorBase -> tast: FSharpImplementationFileContents -> unit
