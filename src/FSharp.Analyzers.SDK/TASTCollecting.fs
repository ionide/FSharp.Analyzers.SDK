namespace FSharp.Analyzers.SDK

open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Symbols.FSharpExprPatterns

module TASTCollecting =

    type TypedTreeCollectorBase() =
        abstract WalkCall: range -> FSharpMemberOrFunctionOrValue -> FSharpExpr list -> unit
        default _.WalkCall _ _ _ = ()
        abstract WalkNewRecord: range -> FSharpType -> unit
        default _.WalkNewRecord _ _ = ()

    let rec visitExpr (handler: TypedTreeCollectorBase) (e: FSharpExpr) =

        match e with
        | AddressOf lvalueExpr -> visitExpr handler lvalueExpr
        | AddressSet(lvalueExpr, rvalueExpr) ->
            visitExpr handler lvalueExpr
            visitExpr handler rvalueExpr
        | Application(funcExpr, _typeArgs, argExprs) ->
            visitExpr handler funcExpr
            visitExprs handler argExprs
        | Call(objExprOpt, memberOrFunc, _typeArgs1, _typeArgs2, argExprs) ->
            handler.WalkCall e.Range memberOrFunc argExprs
            visitObjArg handler objExprOpt
            visitExprs handler argExprs
        | Coerce(_targetType, inpExpr) -> visitExpr handler inpExpr
        | FastIntegerForLoop(startExpr, limitExpr, consumeExpr, _isUp, _debugPointAtFor, _debugPointAtInOrTo) ->
            visitExpr handler startExpr
            visitExpr handler limitExpr
            visitExpr handler consumeExpr
        | ILAsm(_asmCode, _typeArgs, argExprs) -> visitExprs handler argExprs
        | ILFieldGet(objExprOpt, _fieldType, _fieldName) -> visitObjArg handler objExprOpt
        | ILFieldSet(objExprOpt, _fieldType, _fieldName, _valueExpr) -> visitObjArg handler objExprOpt
        | IfThenElse(guardExpr, thenExpr, elseExpr) ->
            visitExpr handler guardExpr
            visitExpr handler thenExpr
            visitExpr handler elseExpr
        | Lambda(_lambdaVar, bodyExpr) -> visitExpr handler bodyExpr
        | Let((_bindingVar, bindingExpr, _debugPointAtBinding), bodyExpr) ->
            visitExpr handler bindingExpr
            visitExpr handler bodyExpr
        | LetRec(recursiveBindings, bodyExpr) ->
            let recursiveBindings' =
                recursiveBindings |> List.map (fun (mfv, expr, _dp) -> (mfv, expr))

            List.iter (snd >> visitExpr handler) recursiveBindings'
            visitExpr handler bodyExpr
        | NewArray(_arrayType, argExprs) -> visitExprs handler argExprs
        | NewDelegate(_delegateType, delegateBodyExpr) -> visitExpr handler delegateBodyExpr
        | NewObject(_objType, _typeArgs, argExprs) -> visitExprs handler argExprs
        | NewRecord(recordType, argExprs) ->
            handler.WalkNewRecord e.Range recordType
            visitExprs handler argExprs
        | NewTuple(_tupleType, argExprs) -> visitExprs handler argExprs
        | NewUnionCase(_unionType, _unionCase, argExprs) -> visitExprs handler argExprs
        | Quote quotedExpr -> visitExpr handler quotedExpr
        | FSharpFieldGet(objExprOpt, _recordOrClassType, _fieldInfo) -> visitObjArg handler objExprOpt
        | FSharpFieldSet(objExprOpt, _recordOrClassType, _fieldInfo, argExpr) ->
            visitObjArg handler objExprOpt
            visitExpr handler argExpr
        | Sequential(firstExpr, secondExpr) ->
            visitExpr handler firstExpr
            visitExpr handler secondExpr
        | TryFinally(bodyExpr, finalizeExpr, _debugPointAtTry, _debugPointAtFinally) ->
            visitExpr handler bodyExpr
            visitExpr handler finalizeExpr
        | TryWith(bodyExpr, _, _, _catchVar, catchExpr, _debugPointAtTry, _debugPointAtWith) ->
            visitExpr handler bodyExpr
            visitExpr handler catchExpr
        | TupleGet(_tupleType, _tupleElemIndex, tupleExpr) -> visitExpr handler tupleExpr
        | DecisionTree(decisionExpr, decisionTargets) ->
            visitExpr handler decisionExpr
            List.iter (snd >> visitExpr handler) decisionTargets
        | DecisionTreeSuccess(_decisionTargetIdx, decisionTargetExprs) -> visitExprs handler decisionTargetExprs
        | TypeLambda(_genericParam, bodyExpr) -> visitExpr handler bodyExpr
        | TypeTest(_ty, inpExpr) -> visitExpr handler inpExpr
        | UnionCaseSet(unionExpr, _unionType, _unionCase, _unionCaseField, valueExpr) ->
            visitExpr handler unionExpr
            visitExpr handler valueExpr
        | UnionCaseGet(unionExpr, _unionType, _unionCase, _unionCaseField) -> visitExpr handler unionExpr
        | UnionCaseTest(unionExpr, _unionType, _unionCase) -> visitExpr handler unionExpr
        | UnionCaseTag(unionExpr, _unionType) -> visitExpr handler unionExpr
        | ObjectExpr(_objType, baseCallExpr, overrides, interfaceImplementations) ->
            visitExpr handler baseCallExpr
            List.iter (visitObjMember handler) overrides
            List.iter (snd >> List.iter (visitObjMember handler)) interfaceImplementations
        | TraitCall(_sourceTypes, _traitName, _typeArgs, _typeInstantiation, _argTypes, argExprs) ->
            visitExprs handler argExprs
        | ValueSet(_valToSet, valueExpr) -> visitExpr handler valueExpr
        | WhileLoop(guardExpr, bodyExpr, _debugPointAtWhile) ->
            visitExpr handler guardExpr
            visitExpr handler bodyExpr
        | BaseValue _baseType -> ()
        | DefaultValue _defaultType -> ()
        | ThisValue _thisType -> ()
        | Const(_constValueObj, _constType) -> ()
        | Value _valueToGet -> ()
        | _ -> ()

    and visitExprs f exprs = List.iter (visitExpr f) exprs

    and visitObjArg f objOpt = Option.iter (visitExpr f) objOpt

    and visitObjMember f memb = visitExpr f memb.Body

    let rec visitDeclaration f d =
        let membersToIgnore = set [ "CompareTo"; "GetHashCode"; "Equals" ]

        let exprTypesToIgnore =
            set [ "Microsoft.FSharp.Core.int"; "Microsoft.FSharp.Core.bool" ]

        match d with
        | FSharpImplementationFileDeclaration.Entity(_e, subDecls) ->
            for subDecl in subDecls do
                visitDeclaration f subDecl
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, _vs, e) ->
            // work around exception from
            // https://github.com/dotnet/fsharp/blob/91ff67b5f698f1929f75e65918e998a2df1c1858/src/Compiler/Symbols/Exprs.fs#L1269
            if
                not v.IsCompilerGenerated
                || not (Set.contains v.CompiledName membersToIgnore)
                || not e.Type.IsAbbreviation
                || not (Set.contains e.Type.BasicQualifiedName exprTypesToIgnore)
            then
                // work around exception from
                // https://github.com/dotnet/fsharp/blob/91ff67b5f698f1929f75e65918e998a2df1c1858/src/Compiler/Symbols/Exprs.fs#L1329
                try
                    visitExpr f e
                with ex ->
                    printfn $"unhandled expression at {e.Range.FileName}:{e.Range.ToString()}"
        | FSharpImplementationFileDeclaration.InitAction e -> visitExpr f e

    let walkTast (walker: TypedTreeCollectorBase) (tast: FSharpImplementationFileContents) : unit =
        tast.Declarations |> List.iter (visitDeclaration walker)
