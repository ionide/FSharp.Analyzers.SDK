namespace FSharp.Analyzers.SDK

module ASTCollecting =
    open FSharp.Compiler.Syntax

    /// The members of this type are called by walkAst.
    /// By overwriting the members for various syntax elements, a custom operation can be executed for them.
    type SyntaxCollectorBase =
        new: unit -> SyntaxCollectorBase

        /// Overwriting this member hooks up a custom operation for a module or namespace syntax element.
        abstract WalkSynModuleOrNamespace: moduleOrNamespace: SynModuleOrNamespace -> unit
        default WalkSynModuleOrNamespace: moduleOrNamespace: FSharp.Compiler.Syntax.SynModuleOrNamespace -> unit

        /// Overwriting this member hooks up a custom operation for a module or namespace syntax element in a signature file.
        abstract WalkSynModuleOrNamespaceSig: moduleOrNamespaceSig: SynModuleOrNamespaceSig -> unit

        default WalkSynModuleOrNamespaceSig:
            moduleOrNamespaceSig: FSharp.Compiler.Syntax.SynModuleOrNamespaceSig -> unit

        /// Overwriting this member hooks up a custom operation for an attribute.
        abstract WalkAttribute: attribute: SynAttribute -> unit
        default WalkAttribute: attribute: FSharp.Compiler.Syntax.SynAttribute -> unit

        /// Overwriting this member hooks up a custom operation for declarations inside a module.
        abstract WalkSynModuleDecl: moduleDecl: SynModuleDecl -> unit
        default WalkSynModuleDecl: moduleDecl: FSharp.Compiler.Syntax.SynModuleDecl -> unit

        /// Overwriting this member hooks up a custom operation for declarations inside a module or namespace in a signature file.
        abstract WalkSynModuleSigDecl: moduleSigDecl: SynModuleSigDecl -> unit
        default WalkSynModuleSigDecl: moduleSigDecl: FSharp.Compiler.Syntax.SynModuleSigDecl -> unit

        /// Overwriting this member hooks up a custom operation for syntax expressions.
        abstract WalkExpr: expr: SynExpr -> unit
        default WalkExpr: expr: FSharp.Compiler.Syntax.SynExpr -> unit

        /// Overwriting this member hooks up a custom operation for type parameters.
        abstract WalkTypar: typar: SynTypar -> unit
        default WalkTypar: typar: FSharp.Compiler.Syntax.SynTypar -> unit

        /// Overwriting this member hooks up a custom operation for explicit declarations of type parameters.
        abstract WalkTyparDecl: typarDecl: SynTyparDecl -> unit
        default WalkTyparDecl: typarDecl: FSharp.Compiler.Syntax.SynTyparDecl -> unit

        /// Overwriting this member hooks up a custom operation for type constraints.
        abstract WalkTypeConstraint: typeConstraint: SynTypeConstraint -> unit
        default WalkTypeConstraint: typeConstraint: FSharp.Compiler.Syntax.SynTypeConstraint -> unit

        /// Overwriting this member hooks up a custom operation for types.
        abstract WalkType: ``type``: SynType -> unit
        default WalkType: ``type``: FSharp.Compiler.Syntax.SynType -> unit

        /// Overwriting this member hooks up a custom operation for member signatures.
        abstract WalkMemberSig: memberSig: SynMemberSig -> unit
        default WalkMemberSig: memberSig: FSharp.Compiler.Syntax.SynMemberSig -> unit

        /// Overwriting this member hooks up a custom operation for F# patterns.
        abstract WalkPat: pat: SynPat -> unit
        default WalkPat: pat: FSharp.Compiler.Syntax.SynPat -> unit

        /// Overwriting this member hooks up a custom operation for type parameters for a member of function.
        abstract WalkValTyparDecls: valTyparDecls: SynValTyparDecls -> unit
        default WalkValTyparDecls: valTyparDecls: FSharp.Compiler.Syntax.SynValTyparDecls -> unit

        /// Overwriting this member hooks up a custom operation for a binding of a 'let' or 'member' declaration.
        abstract WalkBinding: binding: SynBinding -> unit
        default WalkBinding: binding: FSharp.Compiler.Syntax.SynBinding -> unit

        /// Overwriting this member hooks up a custom operation for simple F# patterns.
        abstract WalkSimplePat: simplePat: SynSimplePat -> unit
        default WalkSimplePat: simplePat: FSharp.Compiler.Syntax.SynSimplePat -> unit

        /// Overwriting this member hooks up a custom operation for interface implementations.
        abstract WalkInterfaceImpl: interfaceImpl: SynInterfaceImpl -> unit
        default WalkInterfaceImpl: interfaceImpl: FSharp.Compiler.Syntax.SynInterfaceImpl -> unit

        /// Overwriting this member hooks up a custom operation for clauses in a 'match' expression.
        abstract WalkClause: matchClause: SynMatchClause -> unit
        default WalkClause: matchClause: FSharp.Compiler.Syntax.SynMatchClause -> unit

        /// Overwriting this member hooks up a custom operation for the parts of an interpolated string.
        abstract WalkInterpolatedStringPart: interpolatedStringPart: SynInterpolatedStringPart -> unit

        default WalkInterpolatedStringPart:
            interpolatedStringPart: FSharp.Compiler.Syntax.SynInterpolatedStringPart -> unit

        /// Overwriting this member hooks up a custom operation for units of measure annotations.
        abstract WalkMeasure: measure: SynMeasure -> unit
        default WalkMeasure: measure: FSharp.Compiler.Syntax.SynMeasure -> unit

        /// Overwriting this member hooks up a custom operation for the name of a type definition or module.
        abstract WalkComponentInfo: componentInfo: SynComponentInfo -> unit
        default WalkComponentInfo: componentInfo: FSharp.Compiler.Syntax.SynComponentInfo -> unit

        /// Overwriting this member hooks up a custom operation for the right-hand-side of a type definition.
        abstract WalkTypeDefnSigRepr: typeDefnSigRepr: SynTypeDefnSigRepr -> unit
        default WalkTypeDefnSigRepr: typeDefnSigRepr: FSharp.Compiler.Syntax.SynTypeDefnSigRepr -> unit

        /// Overwriting this member hooks up a custom operation for the right-hand-side of union definition, excluding members.
        abstract WalkUnionCaseType: unionCaseKind: SynUnionCaseKind -> unit
        default WalkUnionCaseType: unionCaseKind: FSharp.Compiler.Syntax.SynUnionCaseKind -> unit

        /// Overwriting this member hooks up a custom operation for the cases of an enum definition.
        abstract WalkEnumCase: enumCase: SynEnumCase -> unit
        default WalkEnumCase: enumCase: FSharp.Compiler.Syntax.SynEnumCase -> unit

        /// Overwriting this member hooks up a custom operation for field declarations in a record or class.
        abstract WalkField: field: SynField -> unit
        default WalkField: field: FSharp.Compiler.Syntax.SynField -> unit

        /// Overwriting this member hooks up a custom operation for the core of a simple type definition.
        abstract WalkTypeDefnSimple: typeDefnSimpleRepr: SynTypeDefnSimpleRepr -> unit
        default WalkTypeDefnSimple: typeDefnSimpleRepr: FSharp.Compiler.Syntax.SynTypeDefnSimpleRepr -> unit

        /// Overwriting this member hooks up a custom operation for a 'val' definition in an abstract slot or a signature file.
        abstract WalkValSig: valSig: SynValSig -> unit
        default WalkValSig: valSig: FSharp.Compiler.Syntax.SynValSig -> unit

        /// Overwriting this member hooks up a custom operation for an element within a type definition.
        abstract WalkMember: memberDefn: SynMemberDefn -> unit
        default WalkMember: memberDefn: FSharp.Compiler.Syntax.SynMemberDefn -> unit

        /// Overwriting this member hooks up a custom operation for the cases of an union definition.
        abstract WalkUnionCase: unionCase: SynUnionCase -> unit
        default WalkUnionCase: unionCase: FSharp.Compiler.Syntax.SynUnionCase -> unit

        /// Overwriting this member hooks up a custom operation for the right hand side of a type or exception declaration.
        abstract WalkTypeDefnRepr: typeDefnRepr: SynTypeDefnRepr -> unit
        default WalkTypeDefnRepr: typeDefnRepr: FSharp.Compiler.Syntax.SynTypeDefnRepr -> unit

        /// Overwriting this member hooks up a custom operation for a type or exception declaration.
        abstract WalkTypeDefn: typeDefn: SynTypeDefn -> unit
        default WalkTypeDefn: typeDefn: FSharp.Compiler.Syntax.SynTypeDefn -> unit

    /// Traverses the whole AST and calls the appropriate members of the given SyntaxCollectorBase
    /// to process the syntax elements.
    val walkAst: walker: SyntaxCollectorBase -> input: ParsedInput -> unit
