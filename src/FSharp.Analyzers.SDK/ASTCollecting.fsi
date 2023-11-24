namespace FSharp.Analyzers.SDK

module ASTCollecting =
    open FSharp.Compiler.Syntax

    /// The members of this type are called by walkAst.
    /// By overwriting the members for various syntax elements, a custom operation can be executed for them.
    type SyntaxCollectorBase =
        new: unit -> SyntaxCollectorBase

        /// Overwriting this member hooks up a custom operation for a module or namespace syntax element.
        abstract WalkSynModuleOrNamespace: path: SyntaxVisitorPath * moduleOrNamespace: SynModuleOrNamespace -> unit
        default WalkSynModuleOrNamespace: path: SyntaxVisitorPath * moduleOrNamespace: SynModuleOrNamespace -> unit

        /// Overwriting this member hooks up a custom operation for a module or namespace syntax element in a signature file.
        abstract WalkSynModuleOrNamespaceSig:
            path: SyntaxVisitorPath * moduleOrNamespaceSig: SynModuleOrNamespaceSig -> unit

        default WalkSynModuleOrNamespaceSig:
            path: SyntaxVisitorPath * moduleOrNamespaceSig: SynModuleOrNamespaceSig -> unit

        /// Overwriting this member hooks up a custom operation for an attribute.
        abstract WalkAttribute: path: SyntaxVisitorPath * attribute: SynAttribute -> unit
        default WalkAttribute: path: SyntaxVisitorPath * attribute: SynAttribute -> unit

        /// Overwriting this member hooks up a custom operation for declarations inside a module.
        abstract WalkSynModuleDecl: path: SyntaxVisitorPath * moduleDecl: SynModuleDecl -> unit
        default WalkSynModuleDecl: path: SyntaxVisitorPath * moduleDecl: SynModuleDecl -> unit

        /// Overwriting this member hooks up a custom operation for declarations inside a module or namespace in a signature file.
        abstract WalkSynModuleSigDecl: path: SyntaxVisitorPath * moduleSigDecl: SynModuleSigDecl -> unit
        default WalkSynModuleSigDecl: path: SyntaxVisitorPath * moduleSigDecl: SynModuleSigDecl -> unit

        /// Overwriting this member hooks up a custom operation for syntax expressions.
        abstract WalkExpr: path: SyntaxVisitorPath * expr: SynExpr -> unit
        default WalkExpr: path: SyntaxVisitorPath * expr: SynExpr -> unit

        /// Overwriting this member hooks up a custom operation for type parameters.
        abstract WalkTypar: path: SyntaxVisitorPath * typar: SynTypar -> unit
        default WalkTypar: path: SyntaxVisitorPath * typar: SynTypar -> unit

        /// Overwriting this member hooks up a custom operation for explicit declarations of type parameters.
        abstract WalkTyparDecl: path: SyntaxVisitorPath * typarDecl: SynTyparDecl -> unit
        default WalkTyparDecl: path: SyntaxVisitorPath * typarDecl: SynTyparDecl -> unit

        /// Overwriting this member hooks up a custom operation for type constraints.
        abstract WalkTypeConstraint: path: SyntaxVisitorPath * typeConstraint: SynTypeConstraint -> unit
        default WalkTypeConstraint: path: SyntaxVisitorPath * typeConstraint: SynTypeConstraint -> unit

        /// Overwriting this member hooks up a custom operation for types.
        abstract WalkType: path: SyntaxVisitorPath * ``type``: SynType -> unit
        default WalkType: path: SyntaxVisitorPath * ``type``: SynType -> unit

        /// Overwriting this member hooks up a custom operation for member signatures.
        abstract WalkMemberSig: path: SyntaxVisitorPath * memberSig: SynMemberSig -> unit
        default WalkMemberSig: path: SyntaxVisitorPath * memberSig: SynMemberSig -> unit

        /// Overwriting this member hooks up a custom operation for F# patterns.
        abstract WalkPat: path: SyntaxVisitorPath * pat: SynPat -> unit
        default WalkPat: path: SyntaxVisitorPath * pat: SynPat -> unit

        /// Overwriting this member hooks up a custom operation for type parameters for a member of function.
        abstract WalkValTyparDecls: path: SyntaxVisitorPath * valTyparDecls: SynValTyparDecls -> unit
        default WalkValTyparDecls: path: SyntaxVisitorPath * valTyparDecls: SynValTyparDecls -> unit

        /// Overwriting this member hooks up a custom operation for a binding of a 'let' or 'member' declaration.
        abstract WalkBinding: path: SyntaxVisitorPath * binding: SynBinding -> unit
        default WalkBinding: path: SyntaxVisitorPath * binding: SynBinding -> unit

        /// Overwriting this member hooks up a custom operation for simple F# patterns.
        abstract WalkSimplePat: path: SyntaxVisitorPath * simplePat: SynSimplePat -> unit
        default WalkSimplePat: path: SyntaxVisitorPath * simplePat: SynSimplePat -> unit

        /// Overwriting this member hooks up a custom operation for interface implementations.
        abstract WalkInterfaceImpl: path: SyntaxVisitorPath * interfaceImpl: SynInterfaceImpl -> unit
        default WalkInterfaceImpl: path: SyntaxVisitorPath * interfaceImpl: SynInterfaceImpl -> unit

        /// Overwriting this member hooks up a custom operation for clauses in a 'match' expression.
        abstract WalkClause: path: SyntaxVisitorPath * matchClause: SynMatchClause -> unit
        default WalkClause: path: SyntaxVisitorPath * matchClause: SynMatchClause -> unit

        /// Overwriting this member hooks up a custom operation for the parts of an interpolated string.
        abstract WalkInterpolatedStringPart:
            path: SyntaxVisitorPath * interpolatedStringPart: SynInterpolatedStringPart -> unit

        default WalkInterpolatedStringPart:
            path: SyntaxVisitorPath * interpolatedStringPart: SynInterpolatedStringPart -> unit

        /// Overwriting this member hooks up a custom operation for units of measure annotations.
        abstract WalkMeasure: path: SyntaxVisitorPath * measure: SynMeasure -> unit
        default WalkMeasure: path: SyntaxVisitorPath * measure: SynMeasure -> unit

        /// Overwriting this member hooks up a custom operation for the name of a type definition or module.
        abstract WalkComponentInfo: path: SyntaxVisitorPath * componentInfo: SynComponentInfo -> unit
        default WalkComponentInfo: path: SyntaxVisitorPath * componentInfo: SynComponentInfo -> unit

        /// Overwriting this member hooks up a custom operation for the right-hand-side of a type definition.
        abstract WalkTypeDefnSigRepr: path: SyntaxVisitorPath * typeDefnSigRepr: SynTypeDefnSigRepr -> unit
        default WalkTypeDefnSigRepr: path: SyntaxVisitorPath * typeDefnSigRepr: SynTypeDefnSigRepr -> unit

        /// Overwriting this member hooks up a custom operation for the right-hand-side of union definition, excluding members.
        abstract WalkUnionCaseType: path: SyntaxVisitorPath * unionCaseKind: SynUnionCaseKind -> unit
        default WalkUnionCaseType: path: SyntaxVisitorPath * unionCaseKind: SynUnionCaseKind -> unit

        /// Overwriting this member hooks up a custom operation for the cases of an enum definition.
        abstract WalkEnumCase: path: SyntaxVisitorPath * enumCase: SynEnumCase -> unit
        default WalkEnumCase: path: SyntaxVisitorPath * enumCase: SynEnumCase -> unit

        /// Overwriting this member hooks up a custom operation for field declarations in a record or class.
        abstract WalkField: path: SyntaxVisitorPath * field: SynField -> unit
        default WalkField: path: SyntaxVisitorPath * field: SynField -> unit

        /// Overwriting this member hooks up a custom operation for the core of a simple type definition.
        abstract WalkTypeDefnSimple: path: SyntaxVisitorPath * typeDefnSimpleRepr: SynTypeDefnSimpleRepr -> unit
        default WalkTypeDefnSimple: path: SyntaxVisitorPath * typeDefnSimpleRepr: SynTypeDefnSimpleRepr -> unit

        /// Overwriting this member hooks up a custom operation for a 'val' definition in an abstract slot or a signature file.
        abstract WalkValSig: path: SyntaxVisitorPath * valSig: SynValSig -> unit
        default WalkValSig: path: SyntaxVisitorPath * valSig: SynValSig -> unit

        /// Overwriting this member hooks up a custom operation for an element within a type definition.
        abstract WalkMember: path: SyntaxVisitorPath * memberDefn: SynMemberDefn -> unit
        default WalkMember: path: SyntaxVisitorPath * memberDefn: SynMemberDefn -> unit

        /// Overwriting this member hooks up a custom operation for the cases of an union definition.
        abstract WalkUnionCase: path: SyntaxVisitorPath * unionCase: SynUnionCase -> unit
        default WalkUnionCase: path: SyntaxVisitorPath * unionCase: SynUnionCase -> unit

        /// Overwriting this member hooks up a custom operation for the right hand side of a type or exception declaration.
        abstract WalkTypeDefnRepr: path: SyntaxVisitorPath * typeDefnRepr: SynTypeDefnRepr -> unit
        default WalkTypeDefnRepr: path: SyntaxVisitorPath * typeDefnRepr: SynTypeDefnRepr -> unit

        /// Overwriting this member hooks up a custom operation for a type or exception declaration.
        abstract WalkTypeDefn: path: SyntaxVisitorPath * typeDefn: SynTypeDefn -> unit
        default WalkTypeDefn: path: SyntaxVisitorPath * typeDefn: SynTypeDefn -> unit

        /// Overwriting this member hooks up a custom operation for a type or exception signature declaration.
        abstract WalkTypeDefnSig: path: SyntaxVisitorPath * typeDefn: SynTypeDefnSig -> unit
        default WalkTypeDefnSig: path: SyntaxVisitorPath * typeDefn: SynTypeDefnSig -> unit

    /// Traverses the whole AST and calls the appropriate members of the given SyntaxCollectorBase
    /// to process the syntax elements.
    val walkAst: walker: SyntaxCollectorBase -> input: ParsedInput -> unit
