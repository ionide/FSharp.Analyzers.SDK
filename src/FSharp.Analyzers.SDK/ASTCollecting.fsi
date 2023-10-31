namespace FSharp.Analyzers.SDK

#nowarn "1182"

module ASTCollecting =
    open FSharp.Compiler.Syntax
    /// A pattern that collects all attributes from a `SynAttributes` into a single flat list
    val (|AllAttrs|): attrs: SynAttributes -> SynAttribute list
    /// An recursive pattern that collect all sequential expressions to avoid StackOverflowException
    val (|Sequentials|_|): e: SynExpr -> SynExpr list option
    val (|ConstructorPats|): SynArgPats -> SynPat list
    /// A pattern that collects all patterns from a `SynSimplePats` into a single flat list
    val (|AllSimplePats|): pats: SynSimplePats -> SynSimplePat list

    type SyntaxCollectorBase =
        new: unit -> SyntaxCollectorBase
        abstract WalkSynModuleOrNamespace: SynModuleOrNamespace -> unit
        default WalkSynModuleOrNamespace: m: FSharp.Compiler.Syntax.SynModuleOrNamespace -> unit
        abstract WalkAttribute: SynAttribute -> unit
        default WalkAttribute: a: FSharp.Compiler.Syntax.SynAttribute -> unit
        abstract WalkSynModuleDecl: SynModuleDecl -> unit
        default WalkSynModuleDecl: m: FSharp.Compiler.Syntax.SynModuleDecl -> unit
        abstract WalkExpr: SynExpr -> unit
        default WalkExpr: s: FSharp.Compiler.Syntax.SynExpr -> unit
        abstract WalkTypar: SynTypar -> unit
        default WalkTypar: s: FSharp.Compiler.Syntax.SynTypar -> unit
        abstract WalkTyparDecl: SynTyparDecl -> unit
        default WalkTyparDecl: s: FSharp.Compiler.Syntax.SynTyparDecl -> unit
        abstract WalkTypeConstraint: SynTypeConstraint -> unit
        default WalkTypeConstraint: s: FSharp.Compiler.Syntax.SynTypeConstraint -> unit
        abstract WalkType: SynType -> unit
        default WalkType: s: FSharp.Compiler.Syntax.SynType -> unit
        abstract WalkMemberSig: SynMemberSig -> unit
        default WalkMemberSig: s: FSharp.Compiler.Syntax.SynMemberSig -> unit
        abstract WalkPat: SynPat -> unit
        default WalkPat: s: FSharp.Compiler.Syntax.SynPat -> unit
        abstract WalkValTyparDecls: SynValTyparDecls -> unit
        default WalkValTyparDecls: s: FSharp.Compiler.Syntax.SynValTyparDecls -> unit
        abstract WalkBinding: SynBinding -> unit
        default WalkBinding: s: FSharp.Compiler.Syntax.SynBinding -> unit
        abstract WalkSimplePat: SynSimplePat -> unit
        default WalkSimplePat: s: FSharp.Compiler.Syntax.SynSimplePat -> unit
        abstract WalkInterfaceImpl: SynInterfaceImpl -> unit
        default WalkInterfaceImpl: s: FSharp.Compiler.Syntax.SynInterfaceImpl -> unit
        abstract WalkClause: SynMatchClause -> unit
        default WalkClause: s: FSharp.Compiler.Syntax.SynMatchClause -> unit
        abstract WalkInterpolatedStringPart: SynInterpolatedStringPart -> unit
        default WalkInterpolatedStringPart: s: FSharp.Compiler.Syntax.SynInterpolatedStringPart -> unit
        abstract WalkMeasure: SynMeasure -> unit
        default WalkMeasure: s: FSharp.Compiler.Syntax.SynMeasure -> unit
        abstract WalkComponentInfo: SynComponentInfo -> unit
        default WalkComponentInfo: s: FSharp.Compiler.Syntax.SynComponentInfo -> unit
        abstract WalkTypeDefnSigRepr: SynTypeDefnSigRepr -> unit
        default WalkTypeDefnSigRepr: s: FSharp.Compiler.Syntax.SynTypeDefnSigRepr -> unit
        abstract WalkUnionCaseType: SynUnionCaseKind -> unit
        default WalkUnionCaseType: s: FSharp.Compiler.Syntax.SynUnionCaseKind -> unit
        abstract WalkEnumCase: SynEnumCase -> unit
        default WalkEnumCase: s: FSharp.Compiler.Syntax.SynEnumCase -> unit
        abstract WalkField: SynField -> unit
        default WalkField: s: FSharp.Compiler.Syntax.SynField -> unit
        abstract WalkTypeDefnSimple: SynTypeDefnSimpleRepr -> unit
        default WalkTypeDefnSimple: s: FSharp.Compiler.Syntax.SynTypeDefnSimpleRepr -> unit
        abstract WalkValSig: SynValSig -> unit
        default WalkValSig: s: FSharp.Compiler.Syntax.SynValSig -> unit
        abstract WalkMember: SynMemberDefn -> unit
        default WalkMember: s: FSharp.Compiler.Syntax.SynMemberDefn -> unit
        abstract WalkUnionCase: SynUnionCase -> unit
        default WalkUnionCase: s: FSharp.Compiler.Syntax.SynUnionCase -> unit
        abstract WalkTypeDefnRepr: SynTypeDefnRepr -> unit
        default WalkTypeDefnRepr: s: FSharp.Compiler.Syntax.SynTypeDefnRepr -> unit
        abstract WalkTypeDefn: SynTypeDefn -> unit
        default WalkTypeDefn: s: FSharp.Compiler.Syntax.SynTypeDefn -> unit

    val walkAst: walker: SyntaxCollectorBase -> input: ParsedInput -> unit
