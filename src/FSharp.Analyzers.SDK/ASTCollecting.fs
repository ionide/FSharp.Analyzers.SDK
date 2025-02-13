namespace FSharp.Analyzers.SDK

module ASTCollecting =
    open FSharp.Compiler.Syntax

    /// A pattern that collects all attributes from a `SynAttributes` into a single flat list
    let (|AllAttrs|) (attrs: SynAttributes) =
        attrs |> List.collect (fun attrList -> attrList.Attributes)

    /// An recursive pattern that collect all sequential expressions to avoid StackOverflowException
    let (|Sequentials|_|) e =
        let rec visit (e: SynExpr) (finalContinuation: SynExpr list -> SynExpr list) : SynExpr list =
            match e with
            | SynExpr.Sequential(expr1 = e1; expr2 = e2) -> visit e2 (fun xs -> e1 :: xs |> finalContinuation)
            | e -> finalContinuation [ e ]

        match e with
        | SynExpr.Sequential(expr1 = e1; expr2 = e2) ->
            let xs = visit e2 id
            Some(e1 :: xs)
        | _ -> None

    let (|ConstructorPats|) =
        function
        | SynArgPats.Pats ps -> ps
        | SynArgPats.NamePatPairs(pats = xs) -> xs |> List.map (fun (_, _, pat) -> pat)

    type SyntaxCollectorBase() =
        abstract WalkSynModuleOrNamespace: path: SyntaxVisitorPath * SynModuleOrNamespace -> unit
        default _.WalkSynModuleOrNamespace(_, _) = ()
        abstract WalkSynModuleOrNamespaceSig: path: SyntaxVisitorPath * SynModuleOrNamespaceSig -> unit
        default _.WalkSynModuleOrNamespaceSig(_, _) = ()
        abstract WalkAttribute: path: SyntaxVisitorPath * SynAttribute -> unit
        default _.WalkAttribute(_, _) = ()
        abstract WalkSynModuleDecl: path: SyntaxVisitorPath * SynModuleDecl -> unit
        default _.WalkSynModuleDecl(_, _) = ()
        abstract WalkSynModuleSigDecl: path: SyntaxVisitorPath * SynModuleSigDecl -> unit
        default _.WalkSynModuleSigDecl(_, _) = ()
        abstract WalkExpr: path: SyntaxVisitorPath * SynExpr -> unit
        default _.WalkExpr(_, _) = ()
        abstract WalkTypar: path: SyntaxVisitorPath * SynTypar -> unit
        default _.WalkTypar(_, _) = ()
        abstract WalkTyparDecl: path: SyntaxVisitorPath * SynTyparDecl -> unit
        default _.WalkTyparDecl(_, _) = ()
        abstract WalkTypeConstraint: path: SyntaxVisitorPath * SynTypeConstraint -> unit
        default _.WalkTypeConstraint(_, _) = ()
        abstract WalkType: path: SyntaxVisitorPath * SynType -> unit
        default _.WalkType(_, _) = ()
        abstract WalkMemberSig: path: SyntaxVisitorPath * SynMemberSig -> unit
        default _.WalkMemberSig(_, _) = ()
        abstract WalkPat: path: SyntaxVisitorPath * SynPat -> unit
        default _.WalkPat(_, _) = ()
        abstract WalkValTyparDecls: path: SyntaxVisitorPath * SynValTyparDecls -> unit
        default _.WalkValTyparDecls(_, _) = ()
        abstract WalkBinding: path: SyntaxVisitorPath * SynBinding -> unit
        default _.WalkBinding(_, _) = ()
        abstract WalkSimplePat: path: SyntaxVisitorPath * SynSimplePat -> unit
        default _.WalkSimplePat(_, _) = ()
        abstract WalkInterfaceImpl: path: SyntaxVisitorPath * SynInterfaceImpl -> unit
        default _.WalkInterfaceImpl(_, _) = ()
        abstract WalkClause: path: SyntaxVisitorPath * SynMatchClause -> unit
        default _.WalkClause(_, _) = ()
        abstract WalkInterpolatedStringPart: path: SyntaxVisitorPath * SynInterpolatedStringPart -> unit
        default _.WalkInterpolatedStringPart(_, _) = ()
        abstract WalkMeasure: path: SyntaxVisitorPath * SynMeasure -> unit
        default _.WalkMeasure(_, _) = ()
        abstract WalkComponentInfo: path: SyntaxVisitorPath * SynComponentInfo -> unit
        default _.WalkComponentInfo(_, _) = ()
        abstract WalkTypeDefnSigRepr: path: SyntaxVisitorPath * SynTypeDefnSigRepr -> unit
        default _.WalkTypeDefnSigRepr(_, _) = ()
        abstract WalkUnionCaseType: path: SyntaxVisitorPath * SynUnionCaseKind -> unit
        default _.WalkUnionCaseType(_, _) = ()
        abstract WalkEnumCase: path: SyntaxVisitorPath * SynEnumCase -> unit
        default _.WalkEnumCase(_, _) = ()
        abstract WalkField: path: SyntaxVisitorPath * SynField -> unit
        default _.WalkField(_, _) = ()
        abstract WalkTypeDefnSimple: path: SyntaxVisitorPath * SynTypeDefnSimpleRepr -> unit
        default _.WalkTypeDefnSimple(_, _) = ()
        abstract WalkValSig: path: SyntaxVisitorPath * SynValSig -> unit
        default _.WalkValSig(_, _) = ()
        abstract WalkMember: path: SyntaxVisitorPath * SynMemberDefn -> unit
        default _.WalkMember(_, _) = ()
        abstract WalkUnionCase: path: SyntaxVisitorPath * SynUnionCase -> unit
        default _.WalkUnionCase(_, _) = ()
        abstract WalkTypeDefnRepr: path: SyntaxVisitorPath * SynTypeDefnRepr -> unit
        default _.WalkTypeDefnRepr(_, _) = ()
        abstract WalkTypeDefn: path: SyntaxVisitorPath * SynTypeDefn -> unit
        default _.WalkTypeDefn(_, _) = ()
        abstract WalkTypeDefnSig: path: SyntaxVisitorPath * typeDefn: SynTypeDefnSig -> unit
        default _.WalkTypeDefnSig(_, _) = ()

    let walkAst (walker: SyntaxCollectorBase) (input: ParsedInput) : unit =

        let rec walkImplFileInput (ParsedImplFileInput(contents = moduleOrNamespaceList)) =
            List.iter walkSynModuleOrNamespace moduleOrNamespaceList

        and walkSigFileInput (ParsedSigFileInput(contents = moduleOrNamespaceList)) =
            List.iter walkSynModuleOrNamespaceSig moduleOrNamespaceList

        and walkSynModuleOrNamespace (SynModuleOrNamespace(decls = decls; attribs = AllAttrs attrs; range = _) as s) =
            walker.WalkSynModuleOrNamespace([], s)
            let path = [ SyntaxNode.SynModuleOrNamespace s ]
            List.iter (walkAttribute path) attrs
            List.iter (walkSynModuleDecl path) decls

        and walkSynModuleOrNamespaceSig
            (SynModuleOrNamespaceSig(decls = decls; attribs = AllAttrs attrs; range = _r) as s)
            =
            walker.WalkSynModuleOrNamespaceSig([], s)
            let path = [ SyntaxNode.SynModuleOrNamespaceSig s ]
            List.iter (walkAttribute path) attrs
            List.iter (walkSynModuleSigDecl path) decls

        and walkAttribute (path: SyntaxVisitorPath) (attr: SynAttribute) = walkExpr path attr.ArgExpr

        and walkTyparDecl
            (path: SyntaxVisitorPath)
            (SynTyparDecl(attributes = AllAttrs attrs; typar = typar; intersectionConstraints = ts))
            =
            List.iter (walkAttribute path) attrs
            walkTypar path typar
            List.iter (walkType path) ts

        and walkTyparDecls (path: SyntaxVisitorPath) (typars: SynTyparDecls) =
            typars.TyparDecls |> List.iter (walkTyparDecl path)
            typars.Constraints |> List.iter (walkTypeConstraint path)

        and walkSynValTyparDecls (path: SyntaxVisitorPath) (SynValTyparDecls(typars, _)) =
            Option.iter (walkTyparDecls path) typars

        and walkTypeConstraint (path: SyntaxVisitorPath) s =
            walker.WalkTypeConstraint(path, s)

            match s with
            | SynTypeConstraint.WhereTyparIsValueType(t, _)
            | SynTypeConstraint.WhereTyparIsReferenceType(t, _)
            | SynTypeConstraint.WhereTyparIsUnmanaged(t, _)
            | SynTypeConstraint.WhereTyparSupportsNull(t, _)
            | SynTypeConstraint.WhereTyparNotSupportsNull(t, _, _)
            | SynTypeConstraint.WhereTyparIsComparable(t, _)
            | SynTypeConstraint.WhereTyparIsEquatable(t, _) -> walkTypar path t
            | SynTypeConstraint.WhereTyparDefaultsToType(t, ty, _)
            | SynTypeConstraint.WhereTyparSubtypeOfType(t, ty, _) ->
                walkTypar path t
                walkType path ty
            | SynTypeConstraint.WhereTyparIsEnum(t, ts, _)
            | SynTypeConstraint.WhereTyparIsDelegate(t, ts, _) ->
                walkTypar path t
                List.iter (walkType path) ts
            | SynTypeConstraint.WhereTyparSupportsMember(t, sign, _) ->
                walkType path t
                walkMemberSig path sign
            | SynTypeConstraint.WhereSelfConstrained(t, _) -> walkType path t

        and walkPat (path: SyntaxVisitorPath) s =
            walker.WalkPat(path, s)

            let nextPath = SyntaxNode.SynPat s :: path

            match s with
            | SynPat.Tuple(elementPats = pats)
            | SynPat.ArrayOrList(_, pats, _)
            | SynPat.Ands(pats, _) -> List.iter (walkPat nextPath) pats
            | SynPat.Named _ -> ()
            | SynPat.Typed(pat, t, _) ->
                walkPat nextPath pat
                walkType nextPath t
            | SynPat.Attrib(pat, AllAttrs attrs, _) ->
                walkPat nextPath pat
                List.iter (walkAttribute nextPath) attrs
            | SynPat.Or(lhsPat = pat1; rhsPat = pat2) -> List.iter (walkPat nextPath) [ pat1; pat2 ]
            | SynPat.LongIdent(typarDecls = typars; argPats = ConstructorPats pats; range = _) ->
                Option.iter (walkSynValTyparDecls nextPath) typars
                List.iter (walkPat nextPath) pats
            | SynPat.Paren(pat, _) -> walkPat nextPath pat
            | SynPat.IsInst(t, _) -> walkType nextPath t
            | SynPat.QuoteExpr(e, _) -> walkExpr nextPath e
            | SynPat.Const _ -> ()
            | SynPat.Wild _ -> ()
            | SynPat.Record _ -> ()
            | SynPat.Null _ -> ()
            | SynPat.OptionalVal _ -> ()
            | SynPat.InstanceMember _ -> ()
            | SynPat.FromParseError _ -> ()
            | SynPat.As(lpat, rpat, _) ->
                walkPat nextPath lpat
                walkPat nextPath rpat
            | SynPat.ListCons(lhsPat = lpat; rhsPat = rpat) ->
                walkPat nextPath lpat
                walkPat nextPath rpat

        and walkTypar (path: SyntaxVisitorPath) (SynTypar _ as s) = walker.WalkTypar(path, s)

        and walkBinding
            (path: SyntaxVisitorPath)
            (SynBinding(attributes = AllAttrs attrs; headPat = pat; returnInfo = returnInfo; expr = e; range = _) as s)
            =
            walker.WalkBinding(path, s)
            let nextPath = SyntaxNode.SynBinding s :: path
            List.iter (walkAttribute nextPath) attrs
            walkPat nextPath pat
            walkExpr nextPath e

            returnInfo
            |> Option.iter (fun (SynBindingReturnInfo(t, _, attrs, _)) ->
                walkType nextPath t
                walkAttributes nextPath attrs
            )

        and walkAttributes (path: SyntaxVisitorPath) (attrs: SynAttributes) =
            List.iter (fun (attrList: SynAttributeList) -> List.iter (walkAttribute path) attrList.Attributes) attrs

        and walkInterfaceImpl (path: SyntaxVisitorPath) (SynInterfaceImpl(bindings = bindings; range = _) as s) =
            walker.WalkInterfaceImpl(path, s)
            List.iter (walkBinding path) bindings

        and walkType (path: SyntaxVisitorPath) s =
            walker.WalkType(path, s)
            let nextPath = SyntaxNode.SynType s :: path

            match s with
            | SynType.Array(_, t, _)
            | SynType.HashConstraint(t, _)
            | SynType.MeasurePower(baseMeasure = t) -> walkType nextPath t
            | SynType.Fun(argType = t1; returnType = t2) ->
                walkType nextPath t1
                walkType nextPath t2
            | SynType.App(typeName = ty; typeArgs = types) ->
                walkType nextPath ty
                List.iter (walkType nextPath) types
            | SynType.LongIdentApp(typeArgs = types) -> List.iter (walkType nextPath) types
            | SynType.Tuple(_, ts, _) ->
                ts
                |> List.iter (
                    function
                    | SynTupleTypeSegment.Type t -> walkType nextPath t
                    | _ -> ()
                )
            | SynType.WithGlobalConstraints(t, typeConstraints, _) ->
                walkType nextPath t
                List.iter (walkTypeConstraint nextPath) typeConstraints
            | SynType.LongIdent _ -> ()
            | SynType.AnonRecd _ -> ()
            | SynType.Var _ -> ()
            | SynType.Anon _ -> ()
            | SynType.StaticConstant _ -> ()
            | SynType.StaticConstantExpr _ -> ()
            | SynType.StaticConstantNamed _ -> ()
            | SynType.StaticConstantNull _ -> ()
            | SynType.WithNull(innerType, _, _, _) -> walkType nextPath innerType
            | SynType.Paren(innerType, _) -> walkType nextPath innerType
            | SynType.SignatureParameter(usedType = t; range = _) -> walkType nextPath t
            | SynType.Or(lhsType = lhs; rhsType = rhs) ->
                walkType nextPath lhs
                walkType nextPath rhs
            | SynType.FromParseError _ -> ()
            | SynType.Intersection(typar = typar; types = types) ->
                Option.iter (walkTypar nextPath) typar
                List.iter (walkType nextPath) types

        and walkClause (path: SyntaxVisitorPath) (SynMatchClause(pat = pat; whenExpr = e1; resultExpr = e2) as s) =
            walker.WalkClause(path, s)
            let nextPath = SyntaxNode.SynMatchClause s :: path
            walkPat nextPath pat
            walkExpr nextPath e2
            e1 |> Option.iter (walkExpr nextPath)

        and walkSimplePats (path: SyntaxVisitorPath) =
            function
            | SynSimplePats.SimplePats(pats = pats; range = _) -> List.iter (walkSimplePat path) pats

        and walkInterpolatedStringPart (path: SyntaxVisitorPath) s =
            walker.WalkInterpolatedStringPart(path, s)

            match s with
            | SynInterpolatedStringPart.FillExpr(expr, _) -> walkExpr path expr
            | SynInterpolatedStringPart.String _ -> ()

        and walkExpr (path: SyntaxVisitorPath) s =
            walker.WalkExpr(path, s)

            let nextPath = SyntaxNode.SynExpr s :: path

            match s with
            | SynExpr.Typed(expr = e) -> walkExpr nextPath e
            | SynExpr.Paren(expr = e)
            | SynExpr.Quote(quotedExpr = e)
            | SynExpr.InferredUpcast(expr = e)
            | SynExpr.InferredDowncast(expr = e)
            | SynExpr.AddressOf(expr = e)
            | SynExpr.DoBang(e, _, _)
            | SynExpr.YieldOrReturn(expr = e)
            | SynExpr.ArrayOrListComputed(expr = e)
            | SynExpr.ComputationExpr(expr = e)
            | SynExpr.Do(e, _)
            | SynExpr.Assert(e, _)
            | SynExpr.Lazy(e, _)
            | SynExpr.YieldOrReturnFrom(_, e, _, _) -> walkExpr nextPath e
            | SynExpr.SequentialOrImplicitYield(expr1 = e1; expr2 = e2; ifNotStmt = ifNotE) ->
                walkExpr nextPath e1
                walkExpr nextPath e2
                walkExpr nextPath ifNotE
            | SynExpr.Lambda(args = pats; body = e; range = _) ->
                walkSimplePats nextPath pats
                walkExpr nextPath e
            | SynExpr.New(targetType = t; expr = e)
            | SynExpr.TypeTest(expr = e; targetType = t)
            | SynExpr.Upcast(expr = e; targetType = t)
            | SynExpr.Downcast(expr = e; targetType = t) ->
                walkExpr nextPath e
                walkType nextPath t
            | SynExpr.Tuple(exprs = es)
            | Sequentials es -> List.iter (walkExpr nextPath) es
            | SynExpr.ArrayOrList(_, es, _) -> List.iter (walkExpr nextPath) es
            | SynExpr.App(funcExpr = e1; argExpr = e2)
            | SynExpr.TryFinally(tryExpr = e1; finallyExpr = e2)
            | SynExpr.While(_, e1, e2, _) -> List.iter (walkExpr nextPath) [ e1; e2 ]
            | SynExpr.Record(recordFields = fields) ->
                fields
                |> List.iter (fun (SynExprRecordField(expr = e)) -> e |> Option.iter (walkExpr nextPath))
            | SynExpr.ObjExpr(objType = ty; argOptions = argOpt; bindings = bindings; extraImpls = ifaces) ->

                argOpt |> Option.iter (fun (e, _) -> walkExpr nextPath e)

                walkType nextPath ty
                List.iter (walkBinding nextPath) bindings
                List.iter (walkInterfaceImpl nextPath) ifaces
            | SynExpr.For(identBody = e1; toBody = e2; doBody = e3; range = _) ->
                List.iter (walkExpr nextPath) [ e1; e2; e3 ]
            | SynExpr.ForEach(pat = pat; enumExpr = e1; bodyExpr = e2) ->
                walkPat nextPath pat
                List.iter (walkExpr nextPath) [ e1; e2 ]
            | SynExpr.MatchLambda(matchClauses = synMatchClauseList) ->
                List.iter (walkClause nextPath) synMatchClauseList
            | SynExpr.Match(expr = e; clauses = synMatchClauseList; range = _) ->
                walkExpr nextPath e
                List.iter (walkClause nextPath) synMatchClauseList
            | SynExpr.TypeApp(expr = e; typeArgs = tys) ->
                List.iter (walkType nextPath) tys
                walkExpr nextPath e
            | SynExpr.LetOrUse(bindings = bindings; body = e; range = _) ->
                List.iter (walkBinding nextPath) bindings
                walkExpr nextPath e
            | SynExpr.TryWith(tryExpr = e; withCases = clauses; range = _) ->
                List.iter (walkClause nextPath) clauses
                walkExpr nextPath e
            | SynExpr.IfThenElse(ifExpr = e1; thenExpr = e2; elseExpr = e3; range = _) ->
                List.iter (walkExpr nextPath) [ e1; e2 ]
                e3 |> Option.iter (walkExpr nextPath)
            | SynExpr.LongIdentSet(expr = e)
            | SynExpr.DotGet(expr = e) -> walkExpr nextPath e
            | SynExpr.DotSet(targetExpr = e1; rhsExpr = e2) ->
                walkExpr nextPath e1
                walkExpr nextPath e2
            | SynExpr.DotIndexedGet(objectExpr = e; indexArgs = args) ->
                walkExpr nextPath e
                walkExpr nextPath args
            | SynExpr.DotIndexedSet(objectExpr = e1; indexArgs = args; valueExpr = e2) ->
                walkExpr nextPath e1
                walkExpr nextPath args
                walkExpr nextPath e2
            | SynExpr.NamedIndexedPropertySet(_, e1, e2, _) -> List.iter (walkExpr nextPath) [ e1; e2 ]
            | SynExpr.DotNamedIndexedPropertySet(targetExpr = e1; argExpr = e2; rhsExpr = e3) ->
                List.iter (walkExpr nextPath) [ e1; e2; e3 ]
            | SynExpr.JoinIn(lhsExpr = e1; rhsExpr = e2) -> List.iter (walkExpr nextPath) [ e1; e2 ]
            | SynExpr.LetOrUseBang(pat = pat; rhs = e1; andBangs = ands; body = e2; range = _) ->
                walkPat nextPath pat
                walkExpr nextPath e1

                for SynExprAndBang(pat = pat; body = body; range = _) in ands do
                    walkPat nextPath pat
                    walkExpr nextPath body

                walkExpr nextPath e2
            | SynExpr.TraitCall(t, sign, e, _) ->
                walkType nextPath t
                walkMemberSig nextPath sign
                walkExpr nextPath e
            | SynExpr.Const(SynConst.Measure(synMeasure = m), _) -> walkMeasure nextPath m
            | SynExpr.Const _ -> ()
            | SynExpr.AnonRecd _ -> ()
            | SynExpr.Sequential _ -> ()
            | SynExpr.Ident _ -> ()
            | SynExpr.LongIdent _ -> ()
            | SynExpr.Set(range = _) -> ()
            | SynExpr.Null _ -> ()
            | SynExpr.ImplicitZero _ -> ()
            | SynExpr.MatchBang(range = _) -> ()
            | SynExpr.LibraryOnlyILAssembly(range = _) -> ()
            | SynExpr.LibraryOnlyStaticOptimization(range = _) -> ()
            | SynExpr.LibraryOnlyUnionCaseFieldGet _ -> ()
            | SynExpr.LibraryOnlyUnionCaseFieldSet(longId = _; range = _) -> ()
            | SynExpr.ArbitraryAfterError _ -> ()
            | SynExpr.FromParseError _ -> ()
            | SynExpr.DiscardAfterMissingQualificationAfterDot(range = _) -> ()
            | SynExpr.Fixed _ -> ()
            | SynExpr.InterpolatedString(contents = parts) ->
                for part in parts do
                    walkInterpolatedStringPart nextPath part
            | SynExpr.IndexFromEnd(itemExpr, _) -> walkExpr nextPath itemExpr
            | SynExpr.IndexRange(expr1 = e1; expr2 = e2; range = _) ->
                Option.iter (walkExpr nextPath) e1
                Option.iter (walkExpr nextPath) e2
            | SynExpr.DebugPoint(innerExpr = expr) -> walkExpr nextPath expr
            | SynExpr.Dynamic(funcExpr = e1; argExpr = e2; range = _) ->
                walkExpr nextPath e1
                walkExpr nextPath e2
            | SynExpr.Typar(t, _) -> walkTypar nextPath t
            | SynExpr.DotLambda(expr = e) -> walkExpr nextPath e
            | SynExpr.WhileBang(whileExpr = whileExpr; doExpr = doExpr) ->
                walkExpr nextPath whileExpr
                walkExpr nextPath doExpr

        and walkMeasure (path: SyntaxVisitorPath) s =
            walker.WalkMeasure(path, s)

            match s with
            | SynMeasure.Product(measure1 = m1; measure2 = m2) ->
                walkMeasure path m1
                walkMeasure path m2
            | SynMeasure.Divide(measure1 = m1; measure2 = m2) ->
                Option.iter (walkMeasure path) m1
                walkMeasure path m2
            | SynMeasure.Named _ -> ()
            | SynMeasure.Seq(ms, _) -> List.iter (walkMeasure path) ms
            | SynMeasure.Power(measure = m) -> walkMeasure path m
            | SynMeasure.Var(ty, _) -> walkTypar path ty
            | SynMeasure.Paren(m, _) -> walkMeasure path m
            | SynMeasure.One _
            | SynMeasure.Anon _ -> ()

        and walkSimplePat (path: SyntaxVisitorPath) s =
            walker.WalkSimplePat(path, s)

            match s with
            | SynSimplePat.Attrib(pat, AllAttrs attrs, _) ->
                walkSimplePat path pat
                List.iter (walkAttribute path) attrs
            | SynSimplePat.Typed(pat, t, _) ->
                walkSimplePat path pat
                walkType path t
            | SynSimplePat.Id _ -> ()

        and walkField (path: SyntaxVisitorPath) (SynField(attributes = AllAttrs attrs; fieldType = t; range = _) as s) =
            walker.WalkField(path, s)
            List.iter (walkAttribute path) attrs
            walkType path t

        and walkValSig
            (path: SyntaxVisitorPath)
            (SynValSig(attributes = AllAttrs attrs; synType = t; arity = SynValInfo(argInfos, argInfo); range = _) as s)
            =
            walker.WalkValSig(path, s)
            let nextPath = SyntaxNode.SynValSig s :: path
            List.iter (walkAttribute nextPath) attrs
            walkType nextPath t

            argInfo :: (argInfos |> List.concat)
            |> List.collect (fun (SynArgInfo(attributes = AllAttrs attrs)) -> attrs)
            |> List.iter (walkAttribute nextPath)

        and walkMemberSig (path: SyntaxVisitorPath) s =
            walker.WalkMemberSig(path, s)
            let nextPath = SyntaxNode.SynMemberSig s :: path

            match s with
            | SynMemberSig.Inherit(t, _)
            | SynMemberSig.Interface(t, _) -> walkType nextPath t
            | SynMemberSig.Member(memberSig = vs) -> walkValSig nextPath vs
            | SynMemberSig.ValField(f, _) -> walkField nextPath f
            | SynMemberSig.NestedType(SynTypeDefnSig(typeInfo = info; typeRepr = repr; members = memberSigs), _) ->

                let isTypeExtensionOrAlias =
                    match repr with
                    | SynTypeDefnSigRepr.Simple(repr = SynTypeDefnSimpleRepr.TypeAbbrev _)
                    | SynTypeDefnSigRepr.ObjectModel(kind = SynTypeDefnKind.Abbrev)
                    | SynTypeDefnSigRepr.ObjectModel(kind = SynTypeDefnKind.Augmentation _) -> true
                    | _ -> false

                walkComponentInfo nextPath isTypeExtensionOrAlias info
                walkTypeDefnSigRepr nextPath repr
                List.iter (walkMemberSig nextPath) memberSigs

        and walkMember (path: SyntaxVisitorPath) s =
            walker.WalkMember(path, s)
            let nextPath = SyntaxNode.SynMemberDefn s :: path

            match s with
            | SynMemberDefn.AbstractSlot(slotSig = valSig) -> walkValSig nextPath valSig
            | SynMemberDefn.Member(binding, _) -> walkBinding nextPath binding
            | SynMemberDefn.ImplicitCtor(attributes = AllAttrs attrs; ctorArgs = pats) ->
                List.iter (walkAttribute nextPath) attrs
                walkPat nextPath pats
            | SynMemberDefn.ImplicitInherit(inheritType = t; inheritArgs = e) ->
                walkType nextPath t
                walkExpr nextPath e
            | SynMemberDefn.LetBindings(bindings = bindings) -> List.iter (walkBinding nextPath) bindings
            | SynMemberDefn.Interface(t, _, members, _) ->
                walkType nextPath t
                members |> Option.iter (List.iter (walkMember nextPath))
            | SynMemberDefn.Inherit(baseType = t) -> t |> Option.iter (walkType nextPath)
            | SynMemberDefn.ValField(field, _) -> walkField nextPath field
            | SynMemberDefn.NestedType(typeDefn = tdef) -> walkTypeDefn nextPath tdef
            | SynMemberDefn.AutoProperty(attributes = AllAttrs attrs; typeOpt = t; synExpr = e; range = _) ->
                List.iter (walkAttribute nextPath) attrs
                Option.iter (walkType nextPath) t
                walkExpr nextPath e
            | SynMemberDefn.Open _ -> ()
            | SynMemberDefn.GetSetMember(memberDefnForGet = getter; memberDefnForSet = setter; range = _) ->
                Option.iter (walkBinding nextPath) getter
                Option.iter (walkBinding nextPath) setter

        and walkEnumCase (path: SyntaxVisitorPath) (SynEnumCase(attributes = AllAttrs attrs; range = _) as s) =
            walker.WalkEnumCase(path, s)
            List.iter (walkAttribute path) attrs

        and walkUnionCaseType (path: SyntaxVisitorPath) s =
            walker.WalkUnionCaseType(path, s)

            match s with
            | SynUnionCaseKind.Fields fields -> List.iter (walkField path) fields
            | SynUnionCaseKind.FullType(t, _) -> walkType path t

        and walkUnionCase
            (path: SyntaxVisitorPath)
            (SynUnionCase(attributes = AllAttrs attrs; caseType = t; range = _) as s)
            =
            walker.WalkUnionCase(path, s)
            List.iter (walkAttribute path) attrs
            walkUnionCaseType path t

        and walkTypeDefnSimple (path: SyntaxVisitorPath) s =
            walker.WalkTypeDefnSimple(path, s)

            match s with
            | SynTypeDefnSimpleRepr.Enum(cases, _) -> List.iter (walkEnumCase path) cases
            | SynTypeDefnSimpleRepr.Union(_, cases, _) -> List.iter (walkUnionCase path) cases
            | SynTypeDefnSimpleRepr.Record(_, fields, _) -> List.iter (walkField path) fields
            | SynTypeDefnSimpleRepr.TypeAbbrev(_, t, _) -> walkType path t
            | SynTypeDefnSimpleRepr.General _ -> ()
            | SynTypeDefnSimpleRepr.LibraryOnlyILAssembly _ -> ()
            | SynTypeDefnSimpleRepr.None _ -> ()
            | SynTypeDefnSimpleRepr.Exception _ -> ()

        and walkComponentInfo
            (path: SyntaxVisitorPath)
            _
            (SynComponentInfo(
                attributes = AllAttrs attrs; typeParams = typars; constraints = constraints; longId = _; range = _) as s)
            =
            walker.WalkComponentInfo(path, s)
            List.iter (walkAttribute path) attrs
            Option.iter (walkTyparDecls path) typars
            List.iter (walkTypeConstraint path) constraints

        and walkTypeDefnRepr (path: SyntaxVisitorPath) s =
            walker.WalkTypeDefnRepr(path, s)

            match s with
            | SynTypeDefnRepr.ObjectModel(_, defns, _) -> List.iter (walkMember path) defns
            | SynTypeDefnRepr.Simple(defn, _) -> walkTypeDefnSimple path defn
            | SynTypeDefnRepr.Exception _ -> ()

        and walkTypeDefnSigRepr (path: SyntaxVisitorPath) s =
            walker.WalkTypeDefnSigRepr(path, s)

            match s with
            | SynTypeDefnSigRepr.ObjectModel(_, defns, _) -> List.iter (walkMemberSig path) defns
            | SynTypeDefnSigRepr.Simple(defn, _) -> walkTypeDefnSimple path defn
            | SynTypeDefnSigRepr.Exception _ -> ()

        and walkTypeDefn
            (path: SyntaxVisitorPath)
            (SynTypeDefn(typeInfo = info; typeRepr = repr; members = members; implicitConstructor = implicitCtor) as s)
            =
            walker.WalkTypeDefn(path, s)
            let nextPath = SyntaxNode.SynTypeDefn s :: path

            let isTypeExtensionOrAlias =
                match repr with
                | SynTypeDefnRepr.ObjectModel(kind = SynTypeDefnKind.Augmentation _)
                | SynTypeDefnRepr.ObjectModel(kind = SynTypeDefnKind.Abbrev)
                | SynTypeDefnRepr.Simple(simpleRepr = SynTypeDefnSimpleRepr.TypeAbbrev _) -> true
                | _ -> false

            walkComponentInfo nextPath isTypeExtensionOrAlias info
            walkTypeDefnRepr nextPath repr
            Option.iter (walkMember nextPath) implicitCtor
            List.iter (walkMember nextPath) members

        and walkTypeDefnSig
            (path: SyntaxVisitorPath)
            (SynTypeDefnSig(typeInfo = info; typeRepr = repr; members = members) as s)
            =
            walker.WalkTypeDefnSig(path, s)
            let nextPath = SyntaxNode.SynTypeDefnSig s :: path

            let isTypeExtensionOrAlias =
                match repr with
                | SynTypeDefnSigRepr.ObjectModel(kind = SynTypeDefnKind.Augmentation _)
                | SynTypeDefnSigRepr.ObjectModel(kind = SynTypeDefnKind.Abbrev)
                | SynTypeDefnSigRepr.Simple(repr = SynTypeDefnSimpleRepr.TypeAbbrev _) -> true
                | _ -> false

            walkComponentInfo nextPath isTypeExtensionOrAlias info
            walkTypeDefnSigRepr nextPath repr
            List.iter (walkMemberSig nextPath) members

        and walkSynModuleDecl (path: SyntaxVisitorPath) (decl: SynModuleDecl) =
            walker.WalkSynModuleDecl(path, decl)
            let nextPath = SyntaxNode.SynModule decl :: path

            match decl with
            | SynModuleDecl.NamespaceFragment fragment -> walkSynModuleOrNamespace fragment
            | SynModuleDecl.NestedModule(moduleInfo = info; decls = modules) ->
                walkComponentInfo nextPath false info
                List.iter (walkSynModuleDecl nextPath) modules
            | SynModuleDecl.Let(bindings = bindings; range = _) -> List.iter (walkBinding nextPath) bindings
            | SynModuleDecl.Expr(expr, _) -> walkExpr nextPath expr
            | SynModuleDecl.Types(types, _) -> List.iter (walkTypeDefn nextPath) types
            | SynModuleDecl.Attributes(attributes = AllAttrs attrs; range = _) ->
                List.iter (walkAttribute nextPath) attrs
            | SynModuleDecl.ModuleAbbrev _ -> ()
            | SynModuleDecl.Exception(exnDefn = SynExceptionDefn(exnRepr = SynExceptionDefnRepr(caseName = unionCase))) ->
                walkUnionCase nextPath unionCase
            | SynModuleDecl.Open _ -> ()
            | SynModuleDecl.HashDirective(range = _) -> ()

        and walkSynModuleSigDecl (path: SyntaxVisitorPath) (decl: SynModuleSigDecl) =
            walker.WalkSynModuleSigDecl(path, decl)
            let nextPath = SyntaxNode.SynModuleSigDecl decl :: path

            match decl with
            | SynModuleSigDecl.ModuleAbbrev _ -> ()
            | SynModuleSigDecl.NestedModule(moduleInfo = info; moduleDecls = decls) ->
                walkComponentInfo nextPath false info
                List.iter (walkSynModuleSigDecl nextPath) decls
            | SynModuleSigDecl.Val(s, _range) -> walkValSig nextPath s
            | SynModuleSigDecl.Types(types, _) -> List.iter (walkTypeDefnSig nextPath) types
            | SynModuleSigDecl.Exception(exnSig = SynExceptionSig(exnRepr = SynExceptionDefnRepr(caseName = unionCase))) ->
                walkUnionCase nextPath unionCase
            | SynModuleSigDecl.Open _ -> ()
            | SynModuleSigDecl.HashDirective _ -> ()
            | SynModuleSigDecl.NamespaceFragment _ -> ()

        match input with
        | ParsedInput.ImplFile input -> walkImplFileInput input
        | ParsedInput.SigFile input -> walkSigFileInput input
