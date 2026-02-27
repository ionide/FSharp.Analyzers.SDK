namespace FSharp.Analyzers.SDK.V1

module TASTCollecting =

    /// Convert the typed tree into SDK-owned V1 types.
    /// This is the main entry point for TAST-based analyzers.
    let convertTast (tast: TypedTreeHandle) : TypedDeclaration list =
        let cache = FSharp.Analyzers.SDK.MigrationV1.ConversionCache()

        tast.Contents.Declarations
        |> List.map (FSharp.Analyzers.SDK.MigrationV1.declarationToV1 cache)

    /// Walk the typed tree and call handler for each expression node.
    let rec visitTypedTree
        (handler: TypedExpr -> unit)
        (declarations: TypedDeclaration list)
        : unit
        =
        for decl in declarations do
            visitDeclaration handler decl

    and private visitDeclaration (handler: TypedExpr -> unit) (decl: TypedDeclaration) : unit =
        match decl with
        | TypedDeclaration.Entity(_, subDecls) ->
            for subDecl in subDecls do
                visitDeclaration handler subDecl
        | TypedDeclaration.MemberOrFunctionOrValue(_, _, body) -> visitExpr handler body
        | TypedDeclaration.InitAction expr -> visitExpr handler expr

    and private visitExpr (handler: TypedExpr -> unit) (expr: TypedExpr) : unit =
        handler expr

        match expr with
        | TypedExpr.AddressOf e -> visitExpr handler e
        | TypedExpr.AddressSet(e1, e2) ->
            visitExpr handler e1
            visitExpr handler e2
        | TypedExpr.Application(funcExpr, _, argExprs) ->
            visitExpr handler funcExpr

            argExprs
            |> List.iter (visitExpr handler)
        | TypedExpr.Call(objExpr, _, _, _, argExprs, _) ->
            objExpr
            |> Option.iter (visitExpr handler)

            argExprs
            |> List.iter (visitExpr handler)
        | TypedExpr.Coerce(_, e) -> visitExpr handler e
        | TypedExpr.FastIntegerForLoop(start, limit, consume, _) ->
            visitExpr handler start
            visitExpr handler limit
            visitExpr handler consume
        | TypedExpr.IfThenElse(guard, thenExpr, elseExpr) ->
            visitExpr handler guard
            visitExpr handler thenExpr
            visitExpr handler elseExpr
        | TypedExpr.Lambda(_, body) -> visitExpr handler body
        | TypedExpr.Let(_, bindingExpr, body) ->
            visitExpr handler bindingExpr
            visitExpr handler body
        | TypedExpr.LetRec(bindings, body) ->
            bindings
            |> List.iter (
                snd
                >> visitExpr handler
            )

            visitExpr handler body
        | TypedExpr.NewArray(_, args) ->
            args
            |> List.iter (visitExpr handler)
        | TypedExpr.NewDelegate(_, body) -> visitExpr handler body
        | TypedExpr.NewObject(_, _, args) ->
            args
            |> List.iter (visitExpr handler)
        | TypedExpr.NewRecord(_, args, _) ->
            args
            |> List.iter (visitExpr handler)
        | TypedExpr.NewTuple(_, args) ->
            args
            |> List.iter (visitExpr handler)
        | TypedExpr.NewUnionCase(_, _, args) ->
            args
            |> List.iter (visitExpr handler)
        | TypedExpr.Quote e -> visitExpr handler e
        | TypedExpr.FieldGet(objExpr, _, _) ->
            objExpr
            |> Option.iter (visitExpr handler)
        | TypedExpr.FieldSet(objExpr, _, _, value) ->
            objExpr
            |> Option.iter (visitExpr handler)

            visitExpr handler value
        | TypedExpr.Sequential(first, second) ->
            visitExpr handler first
            visitExpr handler second
        | TypedExpr.TryFinally(body, finalizer) ->
            visitExpr handler body
            visitExpr handler finalizer
        | TypedExpr.TryWith(body, _, filterExpr, _, catchExpr) ->
            visitExpr handler body
            visitExpr handler filterExpr
            visitExpr handler catchExpr
        | TypedExpr.TupleGet(_, _, tuple) -> visitExpr handler tuple
        | TypedExpr.DecisionTree(decision, targets) ->
            visitExpr handler decision

            targets
            |> List.iter (
                snd
                >> visitExpr handler
            )
        | TypedExpr.DecisionTreeSuccess(_, targetExprs) ->
            targetExprs
            |> List.iter (visitExpr handler)
        | TypedExpr.TypeLambda(_, body) -> visitExpr handler body
        | TypedExpr.TypeTest(_, e) -> visitExpr handler e
        | TypedExpr.UnionCaseSet(unionExpr, _, _, _, value) ->
            visitExpr handler unionExpr
            visitExpr handler value
        | TypedExpr.UnionCaseGet(unionExpr, _, _, _) -> visitExpr handler unionExpr
        | TypedExpr.UnionCaseTest(unionExpr, _, _) -> visitExpr handler unionExpr
        | TypedExpr.UnionCaseTag(unionExpr, _) -> visitExpr handler unionExpr
        | TypedExpr.ObjectExpr(_, baseCall, overrides, interfaceImpls) ->
            visitExpr handler baseCall

            overrides
            |> List.iter (fun o -> visitExpr handler o.Body)

            interfaceImpls
            |> List.iter (fun (_, impls) ->
                impls
                |> List.iter (fun o -> visitExpr handler o.Body)
            )
        | TypedExpr.TraitCall(_, _, _, _, _, argExprs) ->
            argExprs
            |> List.iter (visitExpr handler)
        | TypedExpr.ValueSet(_, value) -> visitExpr handler value
        | TypedExpr.WhileLoop(guard, body) ->
            visitExpr handler guard
            visitExpr handler body
        | TypedExpr.ILAsm(_, _, argExprs) ->
            argExprs
            |> List.iter (visitExpr handler)
        | TypedExpr.ILFieldGet(objExpr, _, _) ->
            objExpr
            |> Option.iter (visitExpr handler)
        | TypedExpr.ILFieldSet(objExpr, _, _, value) ->
            objExpr
            |> Option.iter (visitExpr handler)

            visitExpr handler value
        | TypedExpr.BaseValue _
        | TypedExpr.DefaultValue _
        | TypedExpr.ThisValue _
        | TypedExpr.Const _
        | TypedExpr.Value _
        | TypedExpr.Unknown _ -> ()
