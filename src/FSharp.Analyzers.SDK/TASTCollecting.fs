namespace FSharp.Analyzers.SDK

open FSharp.Compiler.Syntax
open Microsoft.Extensions.Logging
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Symbols.FSharpExprPatterns

module TASTCollecting =

    let mutable logger: ILogger = Abstractions.NullLogger.Instance

    type TypedTreeCollectorBase() =

        abstract WalkAddressOf: lvalueExpr: FSharpExpr -> unit
        default _.WalkAddressOf _ = ()

        abstract WalkAddressSet: lvalueExpr: FSharpExpr -> rvalueExpr: FSharpExpr -> unit
        default _.WalkAddressSet _ _ = ()

        abstract WalkApplication:
            funcExpr: FSharpExpr -> typeArgs: FSharpType list -> argExprs: FSharpExpr list -> unit

        default _.WalkApplication _ _ _ = ()

        abstract WalkCall:
            objExprOpt: FSharpExpr option ->
            memberOrFunc: FSharpMemberOrFunctionOrValue ->
            objExprTypeArgs: FSharpType list ->
            memberOrFuncTypeArgs: FSharpType list ->
            argExprs: FSharpExpr list ->
            exprRange: range ->
                unit

        default _.WalkCall _ _ _ _ _ _ = ()

        abstract WalkCoerce: targetType: FSharpType -> inpExpr: FSharpExpr -> unit
        default _.WalkCoerce _ _ = ()

        abstract WalkFastIntegerForLoop:
            startExpr: FSharpExpr ->
            limitExpr: FSharpExpr ->
            consumeExpr: FSharpExpr ->
            isUp: bool ->
                unit

        default _.WalkFastIntegerForLoop _ _ _ _ = ()

        abstract WalkILAsm:
            asmCode: string -> typeArgs: FSharpType list -> argExprs: FSharpExpr list -> unit

        default _.WalkILAsm _ _ _ = ()

        abstract WalkILFieldGet:
            objExprOpt: FSharpExpr option -> fieldType: FSharpType -> fieldName: string -> unit

        default _.WalkILFieldGet _ _ _ = ()

        abstract WalkILFieldSet:
            objExprOpt: FSharpExpr option ->
            fieldType: FSharpType ->
            fieldName: string ->
            valueExpr: FSharpExpr ->
                unit

        default _.WalkILFieldSet _ _ _ _ = ()

        abstract WalkIfThenElse:
            guardExpr: FSharpExpr -> thenExpr: FSharpExpr -> elseExpr: FSharpExpr -> unit

        default _.WalkIfThenElse _ _ _ = ()

        abstract WalkLambda:
            lambdaVar: FSharpMemberOrFunctionOrValue -> bodyExpr: FSharpExpr -> unit

        default _.WalkLambda _ _ = ()

        abstract WalkLet:
            bindingVar: FSharpMemberOrFunctionOrValue ->
            bindingExpr: FSharpExpr ->
            bodyExpr: FSharpExpr ->
                unit

        default _.WalkLet _ _ _ = ()

        abstract WalkLetRec:
            recursiveBindings: (FSharpMemberOrFunctionOrValue * FSharpExpr) list ->
            bodyExpr: FSharpExpr ->
                unit

        default _.WalkLetRec _ _ = ()

        abstract WalkNewArray: arrayType: FSharpType -> argExprs: FSharpExpr list -> unit
        default _.WalkNewArray _ _ = ()

        abstract WalkNewDelegate: delegateType: FSharpType -> delegateBodyExpr: FSharpExpr -> unit
        default _.WalkNewDelegate _ _ = ()

        abstract WalkNewObject:
            objType: FSharpMemberOrFunctionOrValue ->
            typeArgs: FSharpType list ->
            argExprs: FSharpExpr list ->
                unit

        default _.WalkNewObject _ _ _ = ()

        abstract WalkNewRecord:
            recordType: FSharpType -> argExprs: FSharpExpr list -> exprRange: range -> unit

        default _.WalkNewRecord _ _ _ = ()

        abstract WalkNewTuple: tupleType: FSharpType -> argExprs: FSharpExpr list -> unit
        default _.WalkNewTuple _ _ = ()

        abstract WalkNewUnionCase:
            unionType: FSharpType -> unionCase: FSharpUnionCase -> argExprs: FSharpExpr list -> unit

        default _.WalkNewUnionCase _ _ _ = ()

        abstract WalkQuote: quotedExpr: FSharpExpr -> unit
        default _.WalkQuote _ = ()

        abstract WalkFSharpFieldGet:
            objExprOpt: FSharpExpr option ->
            recordOrClassType: FSharpType ->
            fieldInfo: FSharpField ->
                unit

        default _.WalkFSharpFieldGet _ _ _ = ()

        abstract WalkFSharpFieldSet:
            objExprOp: FSharpExpr option ->
            recordOrClassType: FSharpType ->
            fieldInfo: FSharpField ->
            argExpr: FSharpExpr ->
                unit

        default _.WalkFSharpFieldSet _ _ _ _ = ()

        abstract WalkSequential: firstExpr: FSharpExpr -> secondExpr: FSharpExpr -> unit
        default _.WalkSequential _ _ = ()

        abstract WalkTryFinally: bodyExpr: FSharpExpr -> finalizeExpr: FSharpExpr -> unit
        default _.WalkTryFinally _ _ = ()

        abstract WalkTryWith:
            bodyExpr: FSharpExpr ->
            filterVar: FSharpMemberOrFunctionOrValue ->
            filterExpr: FSharpExpr ->
            catchVar: FSharpMemberOrFunctionOrValue ->
            catchExpr: FSharpExpr ->
                unit

        default _.WalkTryWith _ _ _ _ _ = ()

        abstract WalkTupleGet:
            tupleType: FSharpType -> tupleElemIndex: int -> tupleExpr: FSharpExpr -> unit

        default _.WalkTupleGet _ _ _ = ()

        abstract WalkDecisionTree:
            decisionExpr: FSharpExpr ->
            decisionTargets: (FSharpMemberOrFunctionOrValue list * FSharpExpr) list ->
                unit

        default _.WalkDecisionTree _ _ = ()

        abstract WalkDecisionTreeSuccess:
            decisionTargetIdx: int -> decisionTargetExprs: FSharpExpr list -> unit

        default _.WalkDecisionTreeSuccess _ _ = ()

        abstract WalkTypeLambda:
            genericParam: FSharpGenericParameter list -> bodyExpr: FSharpExpr -> unit

        default _.WalkTypeLambda _ _ = ()

        abstract WalkTypeTest: ty: FSharpType -> inpExpr: FSharpExpr -> unit
        default _.WalkTypeTest _ _ = ()

        abstract WalkUnionCaseSet:
            unionExpr: FSharpExpr ->
            unionType: FSharpType ->
            unionCase: FSharpUnionCase ->
            unionCaseField: FSharpField ->
            valueExpr: FSharpExpr ->
                unit

        default _.WalkUnionCaseSet _ _ _ _ _ = ()

        abstract WalkUnionCaseGet:
            unionExpr: FSharpExpr ->
            unionType: FSharpType ->
            unionCase: FSharpUnionCase ->
            unionCaseField: FSharpField ->
                unit

        default _.WalkUnionCaseGet _ _ _ _ = ()

        abstract WalkUnionCaseTest:
            unionExpr: FSharpExpr -> unionType: FSharpType -> unionCase: FSharpUnionCase -> unit

        default _.WalkUnionCaseTest _ _ _ = ()

        abstract WalkUnionCaseTag: unionExpr: FSharpExpr -> unionType: FSharpType -> unit
        default _.WalkUnionCaseTag _ _ = ()

        abstract WalkObjectExpr:
            objType: FSharpType ->
            baseCallExpr: FSharpExpr ->
            overrides: FSharpObjectExprOverride list ->
            interfaceImplementations: (FSharpType * FSharpObjectExprOverride list) list ->
                unit

        default _.WalkObjectExpr _ _ _ _ = ()

        abstract WalkTraitCall:
            sourceTypes: FSharpType list ->
            traitName: string ->
            typeArgs: SynMemberFlags ->
            typeInstantiation: FSharpType list ->
            argTypes: FSharpType list ->
            argExprs: FSharpExpr list ->
                unit

        default _.WalkTraitCall _ _ _ _ _ _ = ()

        abstract WalkValueSet:
            valToSet: FSharpMemberOrFunctionOrValue -> valueExpr: FSharpExpr -> unit

        default _.WalkValueSet _ _ = ()

        abstract WalkWhileLoop: guardExpr: FSharpExpr -> bodyExpr: FSharpExpr -> unit
        default _.WalkWhileLoop _ _ = ()

        abstract WalkBaseValue: baseType: FSharpType -> unit
        default _.WalkBaseValue _ = ()

        abstract WalkDefaultValue: defaultType: FSharpType -> unit
        default _.WalkDefaultValue _ = ()

        abstract WalkThisValue: thisType: FSharpType -> unit
        default _.WalkThisValue _ = ()

        abstract WalkConst: constValueObj: obj -> constType: FSharpType -> unit
        default _.WalkConst _ _ = ()

        abstract WalkValue: valueToGet: FSharpMemberOrFunctionOrValue -> unit
        default _.WalkValue _ = ()

    let rec visitExpr (handler: TypedTreeCollectorBase) (e: FSharpExpr) =

        match e with
        | AddressOf lvalueExpr ->
            handler.WalkAddressOf lvalueExpr
            visitExpr handler lvalueExpr
        | AddressSet(lvalueExpr, rvalueExpr) ->
            handler.WalkAddressSet lvalueExpr rvalueExpr
            visitExpr handler lvalueExpr
            visitExpr handler rvalueExpr
        | Application(funcExpr, typeArgs, argExprs) ->
            handler.WalkApplication funcExpr typeArgs argExprs
            visitExpr handler funcExpr
            visitExprs handler argExprs
        | Call(objExprOpt, memberOrFunc, objExprTypeArgs, memberOrFuncTypeArgs, argExprs) ->
            handler.WalkCall
                objExprOpt
                memberOrFunc
                objExprTypeArgs
                memberOrFuncTypeArgs
                argExprs
                e.Range

            visitObjArg handler objExprOpt
            visitExprs handler argExprs
        | Coerce(targetType, inpExpr) ->
            handler.WalkCoerce targetType inpExpr
            visitExpr handler inpExpr
        | FastIntegerForLoop(startExpr,
                             limitExpr,
                             consumeExpr,
                             isUp,
                             _debugPointAtFor,
                             _debugPointAtInOrTo) ->
            handler.WalkFastIntegerForLoop startExpr limitExpr consumeExpr isUp
            visitExpr handler startExpr
            visitExpr handler limitExpr
            visitExpr handler consumeExpr
        | ILAsm(asmCode, typeArgs, argExprs) ->
            handler.WalkILAsm asmCode typeArgs argExprs
            visitExprs handler argExprs
        | ILFieldGet(objExprOpt, fieldType, fieldName) ->
            handler.WalkILFieldGet objExprOpt fieldType fieldName
            visitObjArg handler objExprOpt
        | ILFieldSet(objExprOpt, fieldType, fieldName, valueExpr) ->
            handler.WalkILFieldSet objExprOpt fieldType fieldName valueExpr
            visitObjArg handler objExprOpt
        | IfThenElse(guardExpr, thenExpr, elseExpr) ->
            handler.WalkIfThenElse guardExpr thenExpr elseExpr
            visitExpr handler guardExpr
            visitExpr handler thenExpr
            visitExpr handler elseExpr
        | Lambda(lambdaVar, bodyExpr) ->
            handler.WalkLambda lambdaVar bodyExpr
            visitExpr handler bodyExpr
        | Let((bindingVar, bindingExpr, _debugPointAtBinding), bodyExpr) ->
            handler.WalkLet bindingVar bindingExpr bodyExpr
            visitExpr handler bindingExpr
            visitExpr handler bodyExpr
        | LetRec(recursiveBindings, bodyExpr) ->
            let recursiveBindings' =
                recursiveBindings
                |> List.map (fun (mfv, expr, _dp) -> (mfv, expr))

            handler.WalkLetRec recursiveBindings' bodyExpr

            List.iter
                (snd
                 >> visitExpr handler)
                recursiveBindings'

            visitExpr handler bodyExpr
        | NewArray(arrayType, argExprs) ->
            handler.WalkNewArray arrayType argExprs
            visitExprs handler argExprs
        | NewDelegate(delegateType, delegateBodyExpr) ->
            handler.WalkNewDelegate delegateType delegateBodyExpr
            visitExpr handler delegateBodyExpr
        | NewObject(objType, typeArgs, argExprs) ->
            handler.WalkNewObject objType typeArgs argExprs
            visitExprs handler argExprs
        | NewRecord(recordType, argExprs) ->
            handler.WalkNewRecord recordType argExprs e.Range
            visitExprs handler argExprs
        | NewTuple(tupleType, argExprs) ->
            handler.WalkNewTuple tupleType argExprs
            visitExprs handler argExprs
        | NewUnionCase(unionType, unionCase, argExprs) ->
            handler.WalkNewUnionCase unionType unionCase argExprs
            visitExprs handler argExprs
        | Quote quotedExpr ->
            handler.WalkQuote quotedExpr
            visitExpr handler quotedExpr
        | FSharpFieldGet(objExprOpt, recordOrClassType, fieldInfo) ->
            handler.WalkFSharpFieldGet objExprOpt recordOrClassType fieldInfo
            visitObjArg handler objExprOpt
        | FSharpFieldSet(objExprOpt, recordOrClassType, fieldInfo, argExpr) ->
            handler.WalkFSharpFieldSet objExprOpt recordOrClassType fieldInfo argExpr
            visitObjArg handler objExprOpt
            visitExpr handler argExpr
        | Sequential(firstExpr, secondExpr) ->
            handler.WalkSequential firstExpr secondExpr
            visitExpr handler firstExpr
            visitExpr handler secondExpr
        | TryFinally(bodyExpr, finalizeExpr, _debugPointAtTry, _debugPointAtFinally) ->
            handler.WalkTryFinally bodyExpr finalizeExpr
            visitExpr handler bodyExpr
            visitExpr handler finalizeExpr
        | TryWith(bodyExpr,
                  filterVar,
                  filterExpr,
                  catchVar,
                  catchExpr,
                  _debugPointAtTry,
                  _debugPointAtWith) ->
            handler.WalkTryWith bodyExpr filterVar filterExpr catchVar catchExpr
            visitExpr handler bodyExpr
            visitExpr handler catchExpr
        | TupleGet(tupleType, tupleElemIndex, tupleExpr) ->
            handler.WalkTupleGet tupleType tupleElemIndex tupleExpr
            visitExpr handler tupleExpr
        | DecisionTree(decisionExpr, decisionTargets) ->
            handler.WalkDecisionTree decisionExpr decisionTargets
            visitExpr handler decisionExpr

            List.iter
                (snd
                 >> visitExpr handler)
                decisionTargets
        | DecisionTreeSuccess(decisionTargetIdx, decisionTargetExprs) ->
            handler.WalkDecisionTreeSuccess decisionTargetIdx decisionTargetExprs
            visitExprs handler decisionTargetExprs
        | TypeLambda(genericParam, bodyExpr) ->
            handler.WalkTypeLambda genericParam bodyExpr
            visitExpr handler bodyExpr
        | TypeTest(ty, inpExpr) ->
            handler.WalkTypeTest ty inpExpr
            visitExpr handler inpExpr
        | UnionCaseSet(unionExpr, unionType, unionCase, unionCaseField, valueExpr) ->
            handler.WalkUnionCaseSet unionExpr unionType unionCase unionCaseField valueExpr
            visitExpr handler unionExpr
            visitExpr handler valueExpr
        | UnionCaseGet(unionExpr, unionType, unionCase, unionCaseField) ->
            handler.WalkUnionCaseGet unionExpr unionType unionCase unionCaseField
            visitExpr handler unionExpr
        | UnionCaseTest(unionExpr, unionType, unionCase) ->
            handler.WalkUnionCaseTest unionExpr unionType unionCase
            visitExpr handler unionExpr
        | UnionCaseTag(unionExpr, unionType) ->
            handler.WalkUnionCaseTag unionExpr unionType
            visitExpr handler unionExpr
        | ObjectExpr(objType, baseCallExpr, overrides, interfaceImplementations) ->
            handler.WalkObjectExpr objType baseCallExpr overrides interfaceImplementations
            visitExpr handler baseCallExpr
            List.iter (visitObjMember handler) overrides

            List.iter
                (snd
                 >> List.iter (visitObjMember handler))
                interfaceImplementations
        | TraitCall(sourceTypes, traitName, typeArgs, typeInstantiation, argTypes, argExprs) ->
            handler.WalkTraitCall sourceTypes traitName typeArgs typeInstantiation argTypes argExprs
            visitExprs handler argExprs
        | ValueSet(valToSet, valueExpr) ->
            handler.WalkValueSet valToSet valueExpr
            visitExpr handler valueExpr
        | WhileLoop(guardExpr, bodyExpr, _debugPointAtWhile) ->
            handler.WalkWhileLoop guardExpr bodyExpr
            visitExpr handler guardExpr
            visitExpr handler bodyExpr
        | BaseValue baseType -> handler.WalkBaseValue baseType
        | DefaultValue defaultType -> handler.WalkDefaultValue defaultType
        | ThisValue thisType -> handler.WalkThisValue thisType
        | Const(constValueObj, constType) -> handler.WalkConst constValueObj constType
        | Value valueToGet -> handler.WalkValue valueToGet
        | _ -> ()

    and visitExprs f exprs = List.iter (visitExpr f) exprs

    and visitObjArg f objOpt = Option.iter (visitExpr f) objOpt

    and visitObjMember f memb = visitExpr f memb.Body

    let membersToIgnore =
        set
            [
                "CompareTo"
                "GetHashCode"
                "Equals"
            ]

    let exprTypesToIgnore =
        set
            [
                "Microsoft.FSharp.Core.int"
                "Microsoft.FSharp.Core.bool"
            ]

    let rec visitDeclaration f d =

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
                    logger.LogDebug(
                        "unhandled expression at {0}:{1}",
                        e.Range.FileName,
                        e.Range.ToString()
                    )

                    logger.LogDebug("{0}", ex.Message)
                    logger.LogDebug("{0}", ex.StackTrace)
        | FSharpImplementationFileDeclaration.InitAction e -> visitExpr f e

    let walkTast (walker: TypedTreeCollectorBase) (tast: FSharpImplementationFileContents) : unit =
        tast.Declarations
        |> List.iter (visitDeclaration walker)
