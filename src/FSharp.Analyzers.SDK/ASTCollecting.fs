namespace FSharp.Analyzers.SDK

#nowarn "1182"

module ASTCollecting =
    open FSharp.Compiler.Syntax

    /// A pattern that collects all attributes from a `SynAttributes` into a single flat list
    let (|AllAttrs|) (attrs: SynAttributes) =
        attrs |> List.collect (fun attrList -> attrList.Attributes)

    /// An recursive pattern that collect all sequential expressions to avoid StackOverflowException
    let (|Sequentials|_|) e =
        let rec visit (e: SynExpr) (finalContinuation: SynExpr list -> SynExpr list) : SynExpr list =
            match e with
            | SynExpr.Sequential(_, _, e1, e2, _) -> visit e2 (fun xs -> e1 :: xs |> finalContinuation)
            | e -> finalContinuation [ e ]

        match e with
        | SynExpr.Sequential(_, _, e1, e2, _) ->
            let xs = visit e2 id
            Some(e1 :: xs)
        | _ -> None

    let (|ConstructorPats|) =
        function
        | SynArgPats.Pats ps -> ps
        | SynArgPats.NamePatPairs(pats = xs) -> xs |> List.map (fun (_, _, pat) -> pat)

    /// A pattern that collects all patterns from a `SynSimplePats` into a single flat list
    let (|AllSimplePats|) (pats: SynSimplePats) =
        let rec loop acc pat =
            match pat with
            | SynSimplePats.SimplePats(pats = pats) -> acc @ pats

        loop [] pats

    type SyntaxCollectorBase() =
        abstract WalkSynModuleOrNamespace: SynModuleOrNamespace -> unit
        default _.WalkSynModuleOrNamespace moduleOrNamespace = ()
        abstract WalkSynModuleOrNamespaceSig: SynModuleOrNamespaceSig -> unit
        default _.WalkSynModuleOrNamespaceSig moduleOrNamespaceSig = ()
        abstract WalkAttribute: SynAttribute -> unit
        default _.WalkAttribute attribute = ()
        abstract WalkSynModuleDecl: SynModuleDecl -> unit
        default _.WalkSynModuleDecl moduleDecl = ()
        abstract WalkSynModuleSigDecl: SynModuleSigDecl -> unit
        default _.WalkSynModuleSigDecl moduleSigDecl = ()
        abstract WalkExpr: SynExpr -> unit
        default _.WalkExpr expr = ()
        abstract WalkTypar: SynTypar -> unit
        default _.WalkTypar typar = ()
        abstract WalkTyparDecl: SynTyparDecl -> unit
        default _.WalkTyparDecl typarDecl = ()
        abstract WalkTypeConstraint: SynTypeConstraint -> unit
        default _.WalkTypeConstraint typeConstraint = ()
        abstract WalkType: SynType -> unit
        default _.WalkType ``type`` = ()
        abstract WalkMemberSig: SynMemberSig -> unit
        default _.WalkMemberSig memberSig = ()
        abstract WalkPat: SynPat -> unit
        default _.WalkPat pat = ()
        abstract WalkValTyparDecls: SynValTyparDecls -> unit
        default _.WalkValTyparDecls valTyparDecls = ()
        abstract WalkBinding: SynBinding -> unit
        default _.WalkBinding binding = ()
        abstract WalkSimplePat: SynSimplePat -> unit
        default _.WalkSimplePat simplePat = ()
        abstract WalkInterfaceImpl: SynInterfaceImpl -> unit
        default _.WalkInterfaceImpl interfaceImpl = ()
        abstract WalkClause: SynMatchClause -> unit
        default _.WalkClause matchClause = ()
        abstract WalkInterpolatedStringPart: SynInterpolatedStringPart -> unit
        default _.WalkInterpolatedStringPart interpolatedStringPart = ()
        abstract WalkMeasure: SynMeasure -> unit
        default _.WalkMeasure measure = ()
        abstract WalkComponentInfo: SynComponentInfo -> unit
        default _.WalkComponentInfo componentInfo = ()
        abstract WalkTypeDefnSigRepr: SynTypeDefnSigRepr -> unit
        default _.WalkTypeDefnSigRepr typeDefnSigRepr = ()
        abstract WalkUnionCaseType: SynUnionCaseKind -> unit
        default _.WalkUnionCaseType unionCaseKind = ()
        abstract WalkEnumCase: SynEnumCase -> unit
        default _.WalkEnumCase enumCase = ()
        abstract WalkField: SynField -> unit
        default _.WalkField field = ()
        abstract WalkTypeDefnSimple: SynTypeDefnSimpleRepr -> unit
        default _.WalkTypeDefnSimple typeDefnSimpleRepr = ()
        abstract WalkValSig: SynValSig -> unit
        default _.WalkValSig valSig = ()
        abstract WalkMember: SynMemberDefn -> unit
        default _.WalkMember memberDefn = ()
        abstract WalkUnionCase: SynUnionCase -> unit
        default _.WalkUnionCase unionCase = ()
        abstract WalkTypeDefnRepr: SynTypeDefnRepr -> unit
        default _.WalkTypeDefnRepr typeDefnRepr = ()
        abstract WalkTypeDefn: SynTypeDefn -> unit
        default _.WalkTypeDefn typeDefn = ()
        abstract WalkTypeDefnSig: typeDefn: SynTypeDefnSig -> unit
        default _.WalkTypeDefnSig typeDefn = ()

    let walkAst (walker: SyntaxCollectorBase) (input: ParsedInput) : unit =

        let rec walkImplFileInput (ParsedImplFileInput(contents = moduleOrNamespaceList)) =
            List.iter walkSynModuleOrNamespace moduleOrNamespaceList

        and walkSigFileInput (ParsedSigFileInput(contents = moduleOrNamespaceList)) =
            List.iter walkSynModuleOrNamespaceSig moduleOrNamespaceList

        and walkSynModuleOrNamespace (SynModuleOrNamespace(decls = decls; attribs = AllAttrs attrs; range = r) as s) =
            walker.WalkSynModuleOrNamespace s
            List.iter walkAttribute attrs
            List.iter walkSynModuleDecl decls

        and walkSynModuleOrNamespaceSig
            (SynModuleOrNamespaceSig(decls = decls; attribs = AllAttrs attrs; range = _r) as s)
            =
            walker.WalkSynModuleOrNamespaceSig s
            List.iter walkAttribute attrs
            List.iter walkSynModuleSigDecl decls

        and walkAttribute (attr: SynAttribute) = walkExpr attr.ArgExpr

        and walkTyparDecl (SynTyparDecl(attributes = AllAttrs attrs; Item2 = typar)) =
            List.iter walkAttribute attrs
            walkTypar typar

        and walkTyparDecls (typars: SynTyparDecls) =
            typars.TyparDecls |> List.iter walkTyparDecl
            typars.Constraints |> List.iter walkTypeConstraint

        and walkSynValTyparDecls (SynValTyparDecls(typars, _)) = Option.iter walkTyparDecls typars

        and walkTypeConstraint s =
            walker.WalkTypeConstraint s

            match s with
            | SynTypeConstraint.WhereTyparIsValueType(t, r)
            | SynTypeConstraint.WhereTyparIsReferenceType(t, r)
            | SynTypeConstraint.WhereTyparIsUnmanaged(t, r)
            | SynTypeConstraint.WhereTyparSupportsNull(t, r)
            | SynTypeConstraint.WhereTyparIsComparable(t, r)
            | SynTypeConstraint.WhereTyparIsEquatable(t, r) -> walkTypar t
            | SynTypeConstraint.WhereTyparDefaultsToType(t, ty, r)
            | SynTypeConstraint.WhereTyparSubtypeOfType(t, ty, r) ->
                walkTypar t
                walkType ty
            | SynTypeConstraint.WhereTyparIsEnum(t, ts, r)
            | SynTypeConstraint.WhereTyparIsDelegate(t, ts, r) ->
                walkTypar t
                List.iter walkType ts
            | SynTypeConstraint.WhereTyparSupportsMember(t, sign, r) ->
                walkType t
                walkMemberSig sign
            | SynTypeConstraint.WhereSelfConstrained(t, r) -> walkType t

        and walkPat s =
            walker.WalkPat s

            match s with
            | SynPat.Tuple(elementPats = pats; range = r)
            | SynPat.ArrayOrList(_, pats, r)
            | SynPat.Ands(pats, r) -> List.iter walkPat pats
            | SynPat.Named(ident, _, _, r) -> ()
            | SynPat.Typed(pat, t, r) ->
                walkPat pat
                walkType t
            | SynPat.Attrib(pat, AllAttrs attrs, r) ->
                walkPat pat
                List.iter walkAttribute attrs
            | SynPat.Or(pat1, pat2, r, _) -> List.iter walkPat [ pat1; pat2 ]
            | SynPat.LongIdent(typarDecls = typars; argPats = ConstructorPats pats; range = r) ->
                Option.iter walkSynValTyparDecls typars
                List.iter walkPat pats
            | SynPat.Paren(pat, r) -> walkPat pat
            | SynPat.IsInst(t, r) -> walkType t
            | SynPat.QuoteExpr(e, r) -> walkExpr e
            | SynPat.Const(_, r) -> ()
            | SynPat.Wild r -> ()
            | SynPat.Record(_, r) -> ()
            | SynPat.Null r -> ()
            | SynPat.OptionalVal(_, r) -> ()
            | SynPat.DeprecatedCharRange(_, _, r) -> ()
            | SynPat.InstanceMember(_, _, _, accessibility, r) -> ()
            | SynPat.FromParseError(_, r) -> ()
            | SynPat.As(lpat, rpat, r) ->
                walkPat lpat
                walkPat rpat
            | SynPat.ListCons(lpat, rpat, r, _) ->
                walkPat lpat
                walkPat rpat

        and walkTypar (SynTypar _ as s) = walker.WalkTypar s

        and walkBinding
            (SynBinding(attributes = AllAttrs attrs; headPat = pat; returnInfo = returnInfo; expr = e; range = r) as s)
            =
            walker.WalkBinding s
            List.iter walkAttribute attrs
            walkPat pat
            walkExpr e

            returnInfo
            |> Option.iter (fun (SynBindingReturnInfo(t, r, attrs, _)) ->
                walkType t
                walkAttributes attrs
            )

        and walkAttributes (attrs: SynAttributes) =
            List.iter (fun (attrList: SynAttributeList) -> List.iter walkAttribute attrList.Attributes) attrs

        and walkInterfaceImpl (SynInterfaceImpl(bindings = bindings; range = r) as s) =
            walker.WalkInterfaceImpl s
            List.iter walkBinding bindings

        and walkType s =
            walker.WalkType s

            match s with
            | SynType.Array(_, t, r)
            | SynType.HashConstraint(t, r)
            | SynType.MeasurePower(t, _, r) -> walkType t
            | SynType.Fun(t1, t2, r, _) ->
                // | SynType.MeasureDivide(t1, t2, r) ->
                walkType t1
                walkType t2
            | SynType.App(ty, _, types, _, _, _, r) ->
                walkType ty
                List.iter walkType types
            | SynType.LongIdentApp(_, _, _, types, _, _, r) -> List.iter walkType types
            | SynType.Tuple(_, ts, r) ->
                ts
                |> List.iter (
                    function
                    | SynTupleTypeSegment.Type t -> walkType t
                    | _ -> ()
                )
            | SynType.WithGlobalConstraints(t, typeConstraints, r) ->
                walkType t
                List.iter walkTypeConstraint typeConstraints
            | SynType.LongIdent longDotId -> ()
            | SynType.AnonRecd(isStruct, typeNames, r) -> ()
            | SynType.Var(genericName, r) -> ()
            | SynType.Anon r -> ()
            | SynType.StaticConstant(constant, r) -> ()
            | SynType.StaticConstantExpr(expr, r) -> ()
            | SynType.StaticConstantNamed(expr, _, r) -> ()
            | SynType.Paren(innerType, r) -> walkType innerType
            | SynType.SignatureParameter(usedType = t; range = r) -> walkType t
            | SynType.Or(lhs, rhs, r, _) ->
                walkType lhs
                walkType rhs
            | SynType.FromParseError r -> ()

        and walkClause (SynMatchClause(pat, e1, e2, r, _, _) as s) =
            walker.WalkClause s
            walkPat pat
            walkExpr e2
            e1 |> Option.iter walkExpr

        and walkSimplePats =
            function
            | SynSimplePats.SimplePats(pats = pats; range = r) -> List.iter walkSimplePat pats

        and walkInterpolatedStringPart s =
            walker.WalkInterpolatedStringPart s

            match s with
            | SynInterpolatedStringPart.FillExpr(expr, ident) -> walkExpr expr
            | SynInterpolatedStringPart.String(s, r) -> ()

        and walkExpr s =
            walker.WalkExpr s

            match s with
            | SynExpr.Typed(e, _, r) -> walkExpr e
            | SynExpr.Paren(e, _, _, r)
            | SynExpr.Quote(_, _, e, _, r)
            | SynExpr.InferredUpcast(e, r)
            | SynExpr.InferredDowncast(e, r)
            | SynExpr.AddressOf(_, e, _, r)
            | SynExpr.DoBang(e, r)
            | SynExpr.YieldOrReturn(_, e, r)
            | SynExpr.ArrayOrListComputed(_, e, r)
            | SynExpr.ComputationExpr(_, e, r)
            | SynExpr.Do(e, r)
            | SynExpr.Assert(e, r)
            | SynExpr.Lazy(e, r)
            | SynExpr.YieldOrReturnFrom(_, e, r) -> walkExpr e
            | SynExpr.SequentialOrImplicitYield(_, e1, e2, ifNotE, r) ->
                walkExpr e1
                walkExpr e2
                walkExpr ifNotE
            | SynExpr.Lambda(args = pats; body = e; range = r) ->
                walkSimplePats pats
                walkExpr e
            | SynExpr.New(_, t, e, r)
            | SynExpr.TypeTest(e, t, r)
            | SynExpr.Upcast(e, t, r)
            | SynExpr.Downcast(e, t, r) ->
                walkExpr e
                walkType t
            | SynExpr.Tuple(_, es, _, _)
            | Sequentials es -> List.iter walkExpr es //TODO??
            | SynExpr.ArrayOrList(_, es, r) -> List.iter walkExpr es
            | SynExpr.App(_, _, e1, e2, r)
            | SynExpr.TryFinally(e1, e2, r, _, _, _)
            | SynExpr.While(_, e1, e2, r) -> List.iter walkExpr [ e1; e2 ]
            | SynExpr.Record(_, _, fields, r) ->

                fields
                |> List.iter (fun (SynExprRecordField(fieldName = (ident, _); expr = e)) -> e |> Option.iter walkExpr)
            | SynExpr.ObjExpr(ty, argOpt, _, bindings, _, ifaces, _, r) ->

                argOpt |> Option.iter (fun (e, ident) -> walkExpr e)

                walkType ty
                List.iter walkBinding bindings
                List.iter walkInterfaceImpl ifaces
            | SynExpr.For(identBody = e1; toBody = e2; doBody = e3; range = r) -> List.iter walkExpr [ e1; e2; e3 ]
            | SynExpr.ForEach(_, _, _, _, pat, e1, e2, r) ->
                walkPat pat
                List.iter walkExpr [ e1; e2 ]
            | SynExpr.MatchLambda(_, _, synMatchClauseList, _, r) -> List.iter walkClause synMatchClauseList
            | SynExpr.Match(expr = e; clauses = synMatchClauseList; range = r) ->
                walkExpr e
                List.iter walkClause synMatchClauseList
            | SynExpr.TypeApp(e, _, tys, _, _, tr, r) ->
                List.iter walkType tys
                walkExpr e
            | SynExpr.LetOrUse(bindings = bindings; body = e; range = r) ->
                List.iter walkBinding bindings
                walkExpr e
            | SynExpr.TryWith(tryExpr = e; withCases = clauses; range = r) ->
                List.iter walkClause clauses
                walkExpr e
            | SynExpr.IfThenElse(ifExpr = e1; thenExpr = e2; elseExpr = e3; range = r) ->
                List.iter walkExpr [ e1; e2 ]
                e3 |> Option.iter walkExpr
            | SynExpr.LongIdentSet(ident, e, r)
            | SynExpr.DotGet(e, _, ident, r) -> walkExpr e
            | SynExpr.DotSet(e1, idents, e2, r) ->
                walkExpr e1
                walkExpr e2
            | SynExpr.DotIndexedGet(e, args, _, r) ->
                walkExpr e
                walkExpr args
            | SynExpr.DotIndexedSet(e1, args, e2, _, _, r) ->
                walkExpr e1
                walkExpr args
                walkExpr e2
            | SynExpr.NamedIndexedPropertySet(ident, e1, e2, r) -> List.iter walkExpr [ e1; e2 ]
            | SynExpr.DotNamedIndexedPropertySet(e1, ident, e2, e3, r) -> List.iter walkExpr [ e1; e2; e3 ]
            | SynExpr.JoinIn(e1, _, e2, r) -> List.iter walkExpr [ e1; e2 ]
            | SynExpr.LetOrUseBang(pat = pat; rhs = e1; andBangs = ands; body = e2; range = r) ->
                walkPat pat
                walkExpr e1

                for SynExprAndBang(pat = pat; body = body; range = r) in ands do
                    walkPat pat
                    walkExpr body

                walkExpr e2
            | SynExpr.TraitCall(t, sign, e, r) ->
                walkType t
                walkMemberSig sign
                walkExpr e
            | SynExpr.Const(SynConst.Measure(_, _, m), r) -> walkMeasure m
            | SynExpr.Const(_, r) -> ()
            | SynExpr.AnonRecd(isStruct, copyInfo, recordFields, r, trivia) -> ()
            | SynExpr.Sequential(seqPoint, isTrueSeq, expr1, expr2, r) -> ()
            | SynExpr.Ident _ -> ()
            | SynExpr.LongIdent(isOptional, longDotId, altNameRefCell, r) -> ()
            | SynExpr.Set(_, _, r) -> ()
            | SynExpr.Null r -> ()
            | SynExpr.ImplicitZero r -> ()
            | SynExpr.MatchBang(range = r) -> ()
            | SynExpr.LibraryOnlyILAssembly(_, _, _, _, r) -> ()
            | SynExpr.LibraryOnlyStaticOptimization(_, _, _, r) -> ()
            | SynExpr.LibraryOnlyUnionCaseFieldGet(expr, longId, _, r) -> ()
            | SynExpr.LibraryOnlyUnionCaseFieldSet(_, longId, _, _, r) -> ()
            | SynExpr.ArbitraryAfterError(debugStr, r) -> ()
            | SynExpr.FromParseError(expr, r) -> ()
            | SynExpr.DiscardAfterMissingQualificationAfterDot(_, _, r) -> ()
            | SynExpr.Fixed(expr, r) -> ()
            | SynExpr.InterpolatedString(parts, kind, r) ->

                for part in parts do
                    walkInterpolatedStringPart part
            | SynExpr.IndexFromEnd(itemExpr, r) -> walkExpr itemExpr
            | SynExpr.IndexRange(e1, _, e2, _, _, r) ->
                Option.iter walkExpr e1
                Option.iter walkExpr e2
            | SynExpr.DebugPoint(innerExpr = expr) -> walkExpr expr
            | SynExpr.Dynamic(funcExpr = e1; argExpr = e2; range = range) ->
                walkExpr e1
                walkExpr e2
            | SynExpr.Typar(t, r) -> walkTypar t

        and walkMeasure s =
            walker.WalkMeasure s

            match s with
            | SynMeasure.Product(m1, m2, r)
            | SynMeasure.Divide(m1, m2, r) ->
                walkMeasure m1
                walkMeasure m2
            | SynMeasure.Named(longIdent, r) -> ()
            | SynMeasure.Seq(ms, r) -> List.iter walkMeasure ms
            | SynMeasure.Power(m, _, r) -> walkMeasure m
            | SynMeasure.Var(ty, r) -> walkTypar ty
            | SynMeasure.Paren(m, r) -> walkMeasure m
            | SynMeasure.One
            | SynMeasure.Anon _ -> ()

        and walkSimplePat s =
            walker.WalkSimplePat s

            match s with
            | SynSimplePat.Attrib(pat, AllAttrs attrs, r) ->
                walkSimplePat pat
                List.iter walkAttribute attrs
            | SynSimplePat.Typed(pat, t, r) ->
                walkSimplePat pat
                walkType t
            | SynSimplePat.Id(ident, altNameRefCell, isCompilerGenerated, isThisVar, isOptArg, r) -> ()

        and walkField (SynField(attributes = AllAttrs attrs; fieldType = t; range = r) as s) =
            walker.WalkField s
            List.iter walkAttribute attrs
            walkType t

        and walkValSig
            (SynValSig(attributes = AllAttrs attrs; synType = t; arity = SynValInfo(argInfos, argInfo); range = r) as s)
            =
            walker.WalkValSig s
            List.iter walkAttribute attrs
            walkType t

            argInfo :: (argInfos |> List.concat)
            |> List.collect (fun (SynArgInfo(attributes = AllAttrs attrs)) -> attrs)
            |> List.iter walkAttribute

        and walkMemberSig s =
            walker.WalkMemberSig s

            match s with
            | SynMemberSig.Inherit(t, r)
            | SynMemberSig.Interface(t, r) -> walkType t
            | SynMemberSig.Member(vs, _, r, _) -> walkValSig vs
            | SynMemberSig.ValField(f, r) -> walkField f
            | SynMemberSig.NestedType(SynTypeDefnSig(typeInfo = info; typeRepr = repr; members = memberSigs), r) ->

                let isTypeExtensionOrAlias =
                    match repr with
                    | SynTypeDefnSigRepr.Simple(SynTypeDefnSimpleRepr.TypeAbbrev _, _)
                    | SynTypeDefnSigRepr.ObjectModel(SynTypeDefnKind.Abbrev, _, _)
                    | SynTypeDefnSigRepr.ObjectModel(SynTypeDefnKind.Augmentation _, _, _) -> true
                    | _ -> false

                walkComponentInfo isTypeExtensionOrAlias info
                walkTypeDefnSigRepr repr
                List.iter walkMemberSig memberSigs

        and walkMember s =
            walker.WalkMember s

            match s with
            | SynMemberDefn.AbstractSlot(valSig, _, r, _) -> walkValSig valSig
            | SynMemberDefn.Member(binding, r) -> walkBinding binding
            | SynMemberDefn.ImplicitCtor(_, AllAttrs attrs, AllSimplePats pats, _, _, r, _) ->
                List.iter walkAttribute attrs
                List.iter walkSimplePat pats
            | SynMemberDefn.ImplicitInherit(t, e, _, r) ->
                walkType t
                walkExpr e
            | SynMemberDefn.LetBindings(bindings, _, _, r) -> List.iter walkBinding bindings
            | SynMemberDefn.Interface(t, _, members, r) ->
                walkType t
                members |> Option.iter (List.iter walkMember)
            | SynMemberDefn.Inherit(t, _, r) -> walkType t
            | SynMemberDefn.ValField(field, r) -> walkField field
            | SynMemberDefn.NestedType(tdef, _, r) -> walkTypeDefn tdef
            | SynMemberDefn.AutoProperty(attributes = AllAttrs attrs; typeOpt = t; synExpr = e; range = r) ->
                List.iter walkAttribute attrs
                Option.iter walkType t
                walkExpr e
            | SynMemberDefn.Open(longId, r) -> ()
            | SynMemberDefn.GetSetMember(memberDefnForGet = getter; memberDefnForSet = setter; range = range) ->
                Option.iter walkBinding getter
                Option.iter walkBinding setter

        and walkEnumCase (SynEnumCase(attributes = AllAttrs attrs; range = r) as s) =
            walker.WalkEnumCase s
            List.iter walkAttribute attrs

        and walkUnionCaseType s =
            walker.WalkUnionCaseType s

            match s with
            | SynUnionCaseKind.Fields fields -> List.iter walkField fields
            | SynUnionCaseKind.FullType(t, _) -> walkType t

        and walkUnionCase (SynUnionCase(attributes = AllAttrs attrs; caseType = t; range = r) as s) =
            walker.WalkUnionCase s
            List.iter walkAttribute attrs
            walkUnionCaseType t

        and walkTypeDefnSimple s =
            walker.WalkTypeDefnSimple s

            match s with
            | SynTypeDefnSimpleRepr.Enum(cases, r) -> List.iter walkEnumCase cases
            | SynTypeDefnSimpleRepr.Union(_, cases, r) -> List.iter walkUnionCase cases
            | SynTypeDefnSimpleRepr.Record(_, fields, r) -> List.iter walkField fields
            | SynTypeDefnSimpleRepr.TypeAbbrev(_, t, r) -> walkType t
            | SynTypeDefnSimpleRepr.General(_, _, _, _, _, _, _, r) -> ()
            | SynTypeDefnSimpleRepr.LibraryOnlyILAssembly(_, r) -> ()
            | SynTypeDefnSimpleRepr.None r -> ()
            | SynTypeDefnSimpleRepr.Exception _ -> ()

        and walkComponentInfo
            isTypeExtensionOrAlias
            (SynComponentInfo(
                attributes = AllAttrs attrs
                typeParams = typars
                constraints = constraints
                longId = longIdent
                range = r) as s)
            =
            walker.WalkComponentInfo s
            List.iter walkAttribute attrs
            Option.iter walkTyparDecls typars
            List.iter walkTypeConstraint constraints

        and walkTypeDefnRepr s =
            walker.WalkTypeDefnRepr s

            match s with
            | SynTypeDefnRepr.ObjectModel(_, defns, r) -> List.iter walkMember defns
            | SynTypeDefnRepr.Simple(defn, r) -> walkTypeDefnSimple defn
            | SynTypeDefnRepr.Exception _ -> ()

        and walkTypeDefnSigRepr s =
            walker.WalkTypeDefnSigRepr s

            match s with
            | SynTypeDefnSigRepr.ObjectModel(_, defns, _) -> List.iter walkMemberSig defns
            | SynTypeDefnSigRepr.Simple(defn, _) -> walkTypeDefnSimple defn
            | SynTypeDefnSigRepr.Exception _ -> ()

        and walkTypeDefn (SynTypeDefn(info, repr, members, implicitCtor, r, _) as s) =
            walker.WalkTypeDefn s

            let isTypeExtensionOrAlias =
                match repr with
                | SynTypeDefnRepr.ObjectModel(SynTypeDefnKind.Augmentation _, _, _)
                | SynTypeDefnRepr.ObjectModel(SynTypeDefnKind.Abbrev, _, _)
                | SynTypeDefnRepr.Simple(SynTypeDefnSimpleRepr.TypeAbbrev _, _) -> true
                | _ -> false

            walkComponentInfo isTypeExtensionOrAlias info
            walkTypeDefnRepr repr
            Option.iter walkMember implicitCtor
            List.iter walkMember members

        and walkTypeDefnSig (SynTypeDefnSig(info, repr, members, r, _) as s) =
            walker.WalkTypeDefnSig s

            let isTypeExtensionOrAlias =
                match repr with
                | SynTypeDefnSigRepr.ObjectModel(kind = SynTypeDefnKind.Augmentation _)
                | SynTypeDefnSigRepr.ObjectModel(kind = SynTypeDefnKind.Abbrev)
                | SynTypeDefnSigRepr.Simple(repr = SynTypeDefnSimpleRepr.TypeAbbrev _) -> true
                | _ -> false

            walkComponentInfo isTypeExtensionOrAlias info
            walkTypeDefnSigRepr repr
            List.iter walkMemberSig members

        and walkSynModuleDecl (decl: SynModuleDecl) =
            walker.WalkSynModuleDecl decl

            match decl with
            | SynModuleDecl.NamespaceFragment fragment -> walkSynModuleOrNamespace fragment
            | SynModuleDecl.NestedModule(info, _, modules, _, r, _) ->
                walkComponentInfo false info
                List.iter walkSynModuleDecl modules
            | SynModuleDecl.Let(bindings = bindings; range = r) -> List.iter walkBinding bindings
            | SynModuleDecl.Expr(expr, r) -> walkExpr expr
            | SynModuleDecl.Types(types, r) -> List.iter walkTypeDefn types
            | SynModuleDecl.Attributes(attributes = AllAttrs attrs; range = r) -> List.iter walkAttribute attrs
            | SynModuleDecl.ModuleAbbrev(ident, longId, r) -> ()
            | SynModuleDecl.Exception(exnDefn = SynExceptionDefn(exnRepr = SynExceptionDefnRepr(caseName = unionCase))) ->
                walkUnionCase unionCase
            | SynModuleDecl.Open(longDotId, r) -> ()
            | SynModuleDecl.HashDirective(range = r) -> ()

        and walkSynModuleSigDecl (decl: SynModuleSigDecl) =
            walker.WalkSynModuleSigDecl decl

            match decl with
            | SynModuleSigDecl.ModuleAbbrev _ -> ()
            | SynModuleSigDecl.NestedModule _ -> ()
            | SynModuleSigDecl.Val(s, _range) -> walkValSig s
            | SynModuleSigDecl.Types(types, _) -> List.iter walkTypeDefnSig types
            | SynModuleSigDecl.Exception(exnSig = SynExceptionSig(exnRepr = SynExceptionDefnRepr(caseName = unionCase))) ->
                walkUnionCase unionCase
            | SynModuleSigDecl.Open _ -> ()
            | SynModuleSigDecl.HashDirective _ -> ()
            | SynModuleSigDecl.NamespaceFragment _ -> ()

        match input with
        | ParsedInput.ImplFile input -> walkImplFileInput input
        | ParsedInput.SigFile input -> walkSigFileInput input
