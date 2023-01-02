module OptionAnalyzer

open System
open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text

let rec visitExpr memberCallHandler (e: FSharpExpr) =
    match e with
    | AddressOf(lvalueExpr) -> visitExpr memberCallHandler lvalueExpr
    | AddressSet(lvalueExpr, rvalueExpr) ->
        visitExpr memberCallHandler lvalueExpr
        visitExpr memberCallHandler rvalueExpr
    | Application(funcExpr, _, argExprs) ->
        visitExpr memberCallHandler funcExpr
        visitExprs memberCallHandler argExprs
    | Call(objExprOpt, memberOrFunc, _, _, argExprs) ->
        memberCallHandler e.Range memberOrFunc
        visitObjArg memberCallHandler objExprOpt
        visitExprs memberCallHandler argExprs
    | Coerce(_, inpExpr) -> visitExpr memberCallHandler inpExpr
    | FastIntegerForLoop(startExpr, limitExpr, consumeExpr, _, _, _) ->
        visitExpr memberCallHandler startExpr
        visitExpr memberCallHandler limitExpr
        visitExpr memberCallHandler consumeExpr
    | ILAsm(_, _, argExprs) -> visitExprs memberCallHandler argExprs
    | ILFieldGet(objExprOpt, _, _) -> visitObjArg memberCallHandler objExprOpt
    | ILFieldSet(objExprOpt, _, _, _) -> visitObjArg memberCallHandler objExprOpt
    | IfThenElse(guardExpr, thenExpr, elseExpr) ->
        visitExpr memberCallHandler guardExpr
        visitExpr memberCallHandler thenExpr
        visitExpr memberCallHandler elseExpr
    | Lambda(_, bodyExpr) -> visitExpr memberCallHandler bodyExpr
    | Let((_, bindingExpr, _), bodyExpr) ->
        visitExpr memberCallHandler bindingExpr
        visitExpr memberCallHandler bodyExpr
    | LetRec(recursiveBindings, bodyExpr) ->
        let recursiveBindings' =
            recursiveBindings |> List.map (fun (mfv, expr, _) -> (mfv, expr))

        List.iter (snd >> visitExpr memberCallHandler) recursiveBindings'
        visitExpr memberCallHandler bodyExpr
    | NewArray(_, argExprs) -> visitExprs memberCallHandler argExprs
    | NewDelegate(_, delegateBodyExpr) -> visitExpr memberCallHandler delegateBodyExpr
    | NewObject(_, _, argExprs) -> visitExprs memberCallHandler argExprs
    | NewRecord(_, argExprs) -> visitExprs memberCallHandler argExprs
    | NewTuple(_, argExprs) -> visitExprs memberCallHandler argExprs
    | NewUnionCase(_, _, argExprs) -> visitExprs memberCallHandler argExprs
    | Quote(quotedExpr) -> visitExpr memberCallHandler quotedExpr
    | FSharpFieldGet(objExprOpt, _, _) -> visitObjArg memberCallHandler objExprOpt
    | FSharpFieldSet(objExprOpt, _, _, argExpr) ->
        visitObjArg memberCallHandler objExprOpt
        visitExpr memberCallHandler argExpr
    | Sequential(firstExpr, secondExpr) ->
        visitExpr memberCallHandler firstExpr
        visitExpr memberCallHandler secondExpr
    | TryFinally(bodyExpr, finalizeExpr, _, _) ->
        visitExpr memberCallHandler bodyExpr
        visitExpr memberCallHandler finalizeExpr
    | TryWith(bodyExpr, _, _, _, catchExpr, _, _) ->
        visitExpr memberCallHandler bodyExpr
        visitExpr memberCallHandler catchExpr
    | TupleGet(_, _, tupleExpr) -> visitExpr memberCallHandler tupleExpr
    | DecisionTree(decisionExpr, decisionTargets) ->
        visitExpr memberCallHandler decisionExpr
        List.iter (snd >> visitExpr memberCallHandler) decisionTargets
    | DecisionTreeSuccess(_, decisionTargetExprs) -> visitExprs memberCallHandler decisionTargetExprs
    | TypeLambda(_, bodyExpr) -> visitExpr memberCallHandler bodyExpr
    | TypeTest(_, inpExpr) -> visitExpr memberCallHandler inpExpr
    | UnionCaseSet(unionExpr, _, _, _, valueExpr) ->
        visitExpr memberCallHandler unionExpr
        visitExpr memberCallHandler valueExpr
    | UnionCaseGet(unionExpr, _, _, _) -> visitExpr memberCallHandler unionExpr
    | UnionCaseTest(unionExpr, _, _) -> visitExpr memberCallHandler unionExpr
    | UnionCaseTag(unionExpr, _) -> visitExpr memberCallHandler unionExpr
    | ObjectExpr(_, baseCallExpr, overrides, interfaceImplementations) ->
        visitExpr memberCallHandler baseCallExpr
        List.iter (visitObjMember memberCallHandler) overrides
        List.iter (snd >> List.iter (visitObjMember memberCallHandler)) interfaceImplementations
    | TraitCall(_, _, _, _, _, argExprs) -> visitExprs memberCallHandler argExprs
    | ValueSet(_, valueExpr) -> visitExpr memberCallHandler valueExpr
    | WhileLoop(guardExpr, bodyExpr, _) ->
        visitExpr memberCallHandler guardExpr
        visitExpr memberCallHandler bodyExpr
    | BaseValue _ -> ()
    | DefaultValue _ -> ()
    | ThisValue _ -> ()
    | Const(_, _) -> ()
    | Value(_) -> ()
    | _ -> ()

and visitExprs f exprs = List.iter (visitExpr f) exprs

and visitObjArg f objOpt = Option.iter (visitExpr f) objOpt

and visitObjMember f memb = visitExpr f memb.Body

let rec visitDeclaration f d =
    match d with
    | FSharpImplementationFileDeclaration.Entity(_, subDecls) ->
        for subDecl in subDecls do
            visitDeclaration f subDecl
    | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(_, _, e) -> visitExpr f e
    | FSharpImplementationFileDeclaration.InitAction(e) -> visitExpr f e

let notUsed () =
    let option: Option<int> = None
    option.Value

[<Analyzer "OptionAnalyzer">]
let optionValueAnalyzer: Analyzer =
    fun ctx ->
        let state = ResizeArray<range>()

        let handler (range: range) (m: FSharpMemberOrFunctionOrValue) =
            let name = String.Join(".", m.DeclaringEntity.Value.FullName, m.DisplayName)

            if name = "Microsoft.FSharp.Core.FSharpOption`1.Value" then
                state.Add range

        ctx.TypedTree.Declarations |> List.iter (visitDeclaration handler)

        state
        |> Seq.map (fun r -> {
            Type = "Option.Value analyzer"
            Message = "Option.Value shouldn't be used"
            Code = "OV001"
            Severity = Warning
            Range = r
            Fixes = []
        })
        |> Seq.toList
