namespace FSharp.Analyzers.SDK

module ASTCollecting =
    open FSharp.Compiler.Syntax

    /// The members of this type are called by walkAst.
    /// By overwriting the members for various syntax elements, a custom operation can be executed for them.
    type SyntaxCollectorBase =
        new: unit -> SyntaxCollectorBase

        /// Overwriting this member hooks up a custom operation for a module or namespace syntax element.
        abstract WalkSynModuleOrNamespace: SynModuleOrNamespace -> unit
        default WalkSynModuleOrNamespace: m: FSharp.Compiler.Syntax.SynModuleOrNamespace -> unit

        /// Overwriting this member hooks up a custom operation for a module or namespace syntax element in a signature file.
        abstract WalkSynModuleOrNamespaceSig: SynModuleOrNamespaceSig -> unit
        default WalkSynModuleOrNamespaceSig: m: FSharp.Compiler.Syntax.SynModuleOrNamespaceSig -> unit

        /// Overwriting this member hooks up a custom operation for an attribute.
        abstract WalkAttribute: SynAttribute -> unit
        default WalkAttribute: a: FSharp.Compiler.Syntax.SynAttribute -> unit

        /// Overwriting this member hooks up a custom operation for declarations inside a module.
        abstract WalkSynModuleDecl: SynModuleDecl -> unit
        default WalkSynModuleDecl: m: FSharp.Compiler.Syntax.SynModuleDecl -> unit

        /// Overwriting this member hooks up a custom operation for declarations inside a module or namespace in a signature file.
        abstract WalkSynModuleSigDecl: SynModuleSigDecl -> unit
        default WalkSynModuleSigDecl: m: FSharp.Compiler.Syntax.SynModuleSigDecl -> unit

        /// Overwriting this member hooks up a custom operation for syntax expressions.
        abstract WalkExpr: SynExpr -> unit
        default WalkExpr: s: FSharp.Compiler.Syntax.SynExpr -> unit

        /// Overwriting this member hooks up a custom operation for type parameters.
        abstract WalkTypar: SynTypar -> unit
        default WalkTypar: s: FSharp.Compiler.Syntax.SynTypar -> unit

        /// Overwriting this member hooks up a custom operation for explicit declarations of type parameters.
        abstract WalkTyparDecl: SynTyparDecl -> unit
        default WalkTyparDecl: s: FSharp.Compiler.Syntax.SynTyparDecl -> unit

        /// Overwriting this member hooks up a custom operation for type constraints.
        abstract WalkTypeConstraint: SynTypeConstraint -> unit
        default WalkTypeConstraint: s: FSharp.Compiler.Syntax.SynTypeConstraint -> unit

        /// Overwriting this member hooks up a custom operation for types.
        abstract WalkType: SynType -> unit
        default WalkType: s: FSharp.Compiler.Syntax.SynType -> unit

        /// Overwriting this member hooks up a custom operation for member signatures.
        abstract WalkMemberSig: SynMemberSig -> unit
        default WalkMemberSig: s: FSharp.Compiler.Syntax.SynMemberSig -> unit

        /// Overwriting this member hooks up a custom operation for F# patterns.
        abstract WalkPat: SynPat -> unit
        default WalkPat: s: FSharp.Compiler.Syntax.SynPat -> unit

        /// Overwriting this member hooks up a custom operation for type parameters for a member of function.
        abstract WalkValTyparDecls: SynValTyparDecls -> unit
        default WalkValTyparDecls: s: FSharp.Compiler.Syntax.SynValTyparDecls -> unit

        /// Overwriting this member hooks up a custom operation for a binding of a 'let' or 'member' declaration.
        abstract WalkBinding: SynBinding -> unit
        default WalkBinding: s: FSharp.Compiler.Syntax.SynBinding -> unit

        /// Overwriting this member hooks up a custom operation for simple F# patterns.
        abstract WalkSimplePat: SynSimplePat -> unit
        default WalkSimplePat: s: FSharp.Compiler.Syntax.SynSimplePat -> unit

        /// Overwriting this member hooks up a custom operation for interface implementations.
        abstract WalkInterfaceImpl: SynInterfaceImpl -> unit
        default WalkInterfaceImpl: s: FSharp.Compiler.Syntax.SynInterfaceImpl -> unit

        /// Overwriting this member hooks up a custom operation for clauses in a 'match' expression.
        abstract WalkClause: SynMatchClause -> unit
        default WalkClause: s: FSharp.Compiler.Syntax.SynMatchClause -> unit

        /// Overwriting this member hooks up a custom operation for the parts of an interpolated string.
        abstract WalkInterpolatedStringPart: SynInterpolatedStringPart -> unit
        default WalkInterpolatedStringPart: s: FSharp.Compiler.Syntax.SynInterpolatedStringPart -> unit

        /// Overwriting this member hooks up a custom operation for units of measure annotations.
        abstract WalkMeasure: SynMeasure -> unit
        default WalkMeasure: s: FSharp.Compiler.Syntax.SynMeasure -> unit

        /// Overwriting this member hooks up a custom operation for the name of a type definition or module.
        abstract WalkComponentInfo: SynComponentInfo -> unit
        default WalkComponentInfo: s: FSharp.Compiler.Syntax.SynComponentInfo -> unit

        /// Overwriting this member hooks up a custom operation for the right-hand-side of a type definition.
        abstract WalkTypeDefnSigRepr: SynTypeDefnSigRepr -> unit
        default WalkTypeDefnSigRepr: s: FSharp.Compiler.Syntax.SynTypeDefnSigRepr -> unit

        /// Overwriting this member hooks up a custom operation for the right-hand-side of union definition, excluding members.
        abstract WalkUnionCaseType: SynUnionCaseKind -> unit
        default WalkUnionCaseType: s: FSharp.Compiler.Syntax.SynUnionCaseKind -> unit

        /// Overwriting this member hooks up a custom operation for the cases of an enum definition.
        abstract WalkEnumCase: SynEnumCase -> unit
        default WalkEnumCase: s: FSharp.Compiler.Syntax.SynEnumCase -> unit

        /// Overwriting this member hooks up a custom operation for field declarations in a record or class.
        abstract WalkField: SynField -> unit
        default WalkField: s: FSharp.Compiler.Syntax.SynField -> unit

        /// Overwriting this member hooks up a custom operation for the core of a simple type definition.
        abstract WalkTypeDefnSimple: SynTypeDefnSimpleRepr -> unit
        default WalkTypeDefnSimple: s: FSharp.Compiler.Syntax.SynTypeDefnSimpleRepr -> unit

        /// Overwriting this member hooks up a custom operation for a 'val' definition in an abstract slot or a signature file.
        abstract WalkValSig: SynValSig -> unit
        default WalkValSig: s: FSharp.Compiler.Syntax.SynValSig -> unit

        /// Overwriting this member hooks up a custom operation for an element within a type definition.
        abstract WalkMember: SynMemberDefn -> unit
        default WalkMember: s: FSharp.Compiler.Syntax.SynMemberDefn -> unit

        /// Overwriting this member hooks up a custom operation for the cases of an union definition.
        abstract WalkUnionCase: SynUnionCase -> unit
        default WalkUnionCase: s: FSharp.Compiler.Syntax.SynUnionCase -> unit

        /// Overwriting this member hooks up a custom operation for the right hand side of a type or exception declaration.
        abstract WalkTypeDefnRepr: SynTypeDefnRepr -> unit
        default WalkTypeDefnRepr: s: FSharp.Compiler.Syntax.SynTypeDefnRepr -> unit

        /// Overwriting this member hooks up a custom operation for a type or exception declaration.
        abstract WalkTypeDefn: SynTypeDefn -> unit
        default WalkTypeDefn: s: FSharp.Compiler.Syntax.SynTypeDefn -> unit

    /// Traverses the whole AST and calls the appropriate members of the given SyntaxCollectorBase
    /// to process the syntax elements.
    val walkAst: walker: SyntaxCollectorBase -> input: ParsedInput -> unit
