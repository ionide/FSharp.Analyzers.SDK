namespace FSharp.Analyzers.SDK

open Microsoft.Extensions.Logging
open FSharp.Compiler.Symbols
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

module TASTCollecting =

    /// The members of this type are called by walkTast.
    /// By overwriting the members for various tree elements, a custom operation can be executed for them.
    type TypedTreeCollectorBase =
        new: unit -> TypedTreeCollectorBase

        /// Overwriting this member hooks up a custom operation for the AddressOf operator.
        abstract WalkAddressOf: lvalueExpr: FSharpExpr -> unit
        default WalkAddressOf: lvalueExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for the AddressSet operation.
        abstract WalkAddressSet: lvalueExpr: FSharpExpr -> rvalueExpr: FSharpExpr -> unit
        default WalkAddressSet: lvalueExpr: FSharpExpr -> rvalueExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for applications.
        abstract WalkApplication: funcExpr: FSharpExpr -> typeArgs: FSharpType list -> argExprs: FSharpExpr list -> unit
        default WalkApplication: funcExpr: FSharpExpr -> typeArgs: FSharpType list -> argExprs: FSharpExpr list -> unit

        /// Overwriting this member hooks up a custom operation for a call of a member or function.
        abstract WalkCall:
            objExprOpt: FSharpExpr option ->
            memberOrFunc: FSharpMemberOrFunctionOrValue ->
            objExprTypeArgs: FSharpType list ->
            memberOrFuncTypeArgs: FSharpType list ->
            argExprs: FSharpExpr list ->
            exprRange: range ->
                unit

        default WalkCall:
            objExprOpt: FSharpExpr option ->
            memberOrFunc: FSharpMemberOrFunctionOrValue ->
            objTypeArgs: FSharpType list ->
            memberOrFuncTypeArgs: FSharpType list ->
            argExprs: FSharpExpr list ->
            exprRange: range ->
                unit

        /// Overwriting this member hooks up a custom operation for type coercion.
        abstract WalkCoerce: targetType: FSharpType -> inpExpr: FSharpExpr -> unit
        default WalkCoerce: targetType: FSharpType -> inpExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for fast integer loops.
        abstract WalkFastIntegerForLoop:
            startExpr: FSharpExpr -> limitExpr: FSharpExpr -> consumeExpr: FSharpExpr -> isUp: bool -> unit

        default WalkFastIntegerForLoop:
            startExpr: FSharpExpr -> limitExpr: FSharpExpr -> consumeExpr: FSharpExpr -> isUp: bool -> unit

        /// Overwriting this member hooks up a custom operation for ILAsm code.
        abstract WalkILAsm: asmCode: string -> typeArgs: FSharpType list -> argExprs: FSharpExpr list -> unit
        default WalkILAsm: asmCode: string -> typeArgs: FSharpType list -> argExprs: FSharpExpr list -> unit

        /// Overwriting this member hooks up a custom operation for ILFieldGet expressions.
        abstract WalkILFieldGet: objExprOpt: FSharpExpr option -> fieldType: FSharpType -> fieldName: string -> unit
        default WalkILFieldGet: objExprOpt: FSharpExpr option -> fieldType: FSharpType -> fieldName: string -> unit

        /// Overwriting this member hooks up a custom operation for ILFieldSet expressions.
        abstract WalkILFieldSet:
            objExprOpt: FSharpExpr option -> fieldType: FSharpType -> fieldName: string -> valueExpr: FSharpExpr -> unit

        default WalkILFieldSet:
            objExprOpt: FSharpExpr option -> fieldType: FSharpType -> fieldName: string -> valueExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for if-then-else expressions.
        abstract WalkIfThenElse: guardExpr: FSharpExpr -> thenExpr: FSharpExpr -> elseExpr: FSharpExpr -> unit
        default WalkIfThenElse: guardExpr: FSharpExpr -> thenExpr: FSharpExpr -> elseExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for lambda expressions.
        abstract WalkLambda: lambdaVar: FSharpMemberOrFunctionOrValue -> bodyExpr: FSharpExpr -> unit
        default WalkLambda: lambdaVar: FSharpMemberOrFunctionOrValue -> bodyExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for let expressions.
        abstract WalkLet:
            bindingVar: FSharpMemberOrFunctionOrValue -> bindingExpr: FSharpExpr -> bodyExpr: FSharpExpr -> unit

        default WalkLet:
            bindingVar: FSharpMemberOrFunctionOrValue -> bindingExpr: FSharpExpr -> bodyExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for let-rec expressions.
        abstract WalkLetRec:
            recursiveBindings: (FSharpMemberOrFunctionOrValue * FSharpExpr) list -> bodyExpr: FSharpExpr -> unit

        default WalkLetRec:
            recursiveBindings: (FSharpMemberOrFunctionOrValue * FSharpExpr) list -> bodyExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for a new array instance.
        abstract WalkNewArray: arrayType: FSharpType -> argExprs: FSharpExpr list -> unit
        default WalkNewArray: arrayType: FSharpType -> argExprs: FSharpExpr list -> unit

        /// Overwriting this member hooks up a custom operation for a new delegate instance.
        abstract WalkNewDelegate: delegateType: FSharpType -> delegateBodyExpr: FSharpExpr -> unit
        default WalkNewDelegate: delegateType: FSharpType -> delegateBodyExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for a new object.
        abstract WalkNewObject:
            objType: FSharpMemberOrFunctionOrValue -> typeArgs: FSharpType list -> argExprs: FSharpExpr list -> unit

        default WalkNewObject:
            objType: FSharpMemberOrFunctionOrValue -> typeArgs: FSharpType list -> argExprs: FSharpExpr list -> unit

        /// Overwriting this member hooks up a custom operation for the creation of a new record instance.
        abstract WalkNewRecord: recordType: FSharpType -> argExprs: FSharpExpr list -> exprRange: range -> unit
        default WalkNewRecord: recordType: FSharpType -> argExprs: FSharpExpr list -> exprRange: range -> unit

        /// Overwriting this member hooks up a custom operation for the creation of a new tuple instance.
        abstract WalkNewTuple: tupleType: FSharpType -> argExprs: FSharpExpr list -> unit
        default WalkNewTuple: tupleType: FSharpType -> argExprs: FSharpExpr list -> unit

        /// Overwriting this member hooks up a custom operation for the creation of a new union case.
        abstract WalkNewUnionCase:
            unionType: FSharpType -> unionCase: FSharpUnionCase -> argExprs: FSharpExpr list -> unit

        default WalkNewUnionCase:
            unionType: FSharpType -> unionCase: FSharpUnionCase -> argExprs: FSharpExpr list -> unit

        /// Overwriting this member hooks up a custom operation for quotation expressions.
        abstract WalkQuote: quotedExpr: FSharpExpr -> unit
        default WalkQuote: quotedExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for field-get expressions.
        abstract WalkFSharpFieldGet:
            objExprOpt: FSharpExpr option -> recordOrClassType: FSharpType -> fieldInfo: FSharpField -> unit

        default WalkFSharpFieldGet:
            objExprOpt: FSharpExpr option -> recordOrClassType: FSharpType -> fieldInfo: FSharpField -> unit

        /// Overwriting this member hooks up a custom operation for field-set expressions.
        abstract WalkFSharpFieldSet:
            objExprOp: FSharpExpr option ->
            recordOrClassType: FSharpType ->
            fieldInfo: FSharpField ->
            argExpr: FSharpExpr ->
                unit

        default WalkFSharpFieldSet:
            objExprOp: FSharpExpr option ->
            recordOrClassType: FSharpType ->
            fieldInfo: FSharpField ->
            argExpr: FSharpExpr ->
                unit

        /// Overwriting this member hooks up a custom operation for sequential expressions.
        abstract WalkSequential: firstExpr: FSharpExpr -> secondExpr: FSharpExpr -> unit
        default WalkSequential: firstExpr: FSharpExpr -> secondExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for try-finally expressions.
        abstract WalkTryFinally: bodyExpr: FSharpExpr -> finalizeExpr: FSharpExpr -> unit
        default WalkTryFinally: bodyExpr: FSharpExpr -> finalizeExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for try-with expressions.
        abstract WalkTryWith:
            bodyExpr: FSharpExpr ->
            filterVar: FSharpMemberOrFunctionOrValue ->
            filterExpr: FSharpExpr ->
            catchVar: FSharpMemberOrFunctionOrValue ->
            catchExpr: FSharpExpr ->
                unit

        default WalkTryWith:
            bodyExpr: FSharpExpr ->
            filterVar: FSharpMemberOrFunctionOrValue ->
            filterExpr: FSharpExpr ->
            catchVar: FSharpMemberOrFunctionOrValue ->
            catchExpr: FSharpExpr ->
                unit

        /// Overwriting this member hooks up a custom operation for tuple-get expressions.
        abstract WalkTupleGet: tupleType: FSharpType -> tupleElemIndex: int -> tupleExpr: FSharpExpr -> unit
        default WalkTupleGet: tupleType: FSharpType -> tupleElemIndex: int -> tupleExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for decision trees.
        abstract WalkDecisionTree:
            decisionExpr: FSharpExpr -> decisionTargets: (FSharpMemberOrFunctionOrValue list * FSharpExpr) list -> unit

        default WalkDecisionTree:
            decisionExpr: FSharpExpr -> decisionTargets: (FSharpMemberOrFunctionOrValue list * FSharpExpr) list -> unit

        /// Overwriting this member hooks up a custom operation for decision tree success expressions.
        abstract WalkDecisionTreeSuccess: decisionTargetIdx: int -> decisionTargetExprs: FSharpExpr list -> unit
        default WalkDecisionTreeSuccess: decisionTargetIdx: int -> decisionTargetExprs: FSharpExpr list -> unit

        /// Overwriting this member hooks up a custom operation for type lambdas.
        abstract WalkTypeLambda: genericParam: FSharpGenericParameter list -> bodyExpr: FSharpExpr -> unit
        default WalkTypeLambda: genericParam: FSharpGenericParameter list -> bodyExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for type tests.
        abstract WalkTypeTest: ty: FSharpType -> inpExpr: FSharpExpr -> unit
        default WalkTypeTest: ty: FSharpType -> inpExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for union case set expressions.
        abstract WalkUnionCaseSet:
            unionExpr: FSharpExpr ->
            unionType: FSharpType ->
            unionCase: FSharpUnionCase ->
            unionCaseField: FSharpField ->
            valueExpr: FSharpExpr ->
                unit

        default WalkUnionCaseSet:
            unionExpr: FSharpExpr ->
            unionType: FSharpType ->
            unionCase: FSharpUnionCase ->
            unionCaseField: FSharpField ->
            valueExpr: FSharpExpr ->
                unit

        /// Overwriting this member hooks up a custom operation for union case get expressions.
        abstract WalkUnionCaseGet:
            unionExpr: FSharpExpr ->
            unionType: FSharpType ->
            unionCase: FSharpUnionCase ->
            unionCaseField: FSharpField ->
                unit

        default WalkUnionCaseGet:
            unionExpr: FSharpExpr ->
            unionType: FSharpType ->
            unionCase: FSharpUnionCase ->
            unionCaseField: FSharpField ->
                unit

        /// Overwriting this member hooks up a custom operation for union case test expressions.
        abstract WalkUnionCaseTest: unionExpr: FSharpExpr -> unionType: FSharpType -> unionCase: FSharpUnionCase -> unit
        default WalkUnionCaseTest: unionExpr: FSharpExpr -> unionType: FSharpType -> unionCase: FSharpUnionCase -> unit

        /// Overwriting this member hooks up a custom operation for union case tag expressions.
        abstract WalkUnionCaseTag: unionExpr: FSharpExpr -> unionType: FSharpType -> unit
        default WalkUnionCaseTag: unionExpr: FSharpExpr -> unionType: FSharpType -> unit

        /// Overwriting this member hooks up a custom operation for object expressions.
        abstract WalkObjectExpr:
            objType: FSharpType ->
            baseCallExpr: FSharpExpr ->
            overrides: FSharpObjectExprOverride list ->
            interfaceImplementations: (FSharpType * FSharpObjectExprOverride list) list ->
                unit

        default WalkObjectExpr:
            objType: FSharpType ->
            baseCallExpr: FSharpExpr ->
            overrides: FSharpObjectExprOverride list ->
            interfaceImplementations: (FSharpType * FSharpObjectExprOverride list) list ->
                unit

        /// Overwriting this member hooks up a custom operation for trait calls.
        abstract WalkTraitCall:
            sourceTypes: FSharpType list ->
            traitName: string ->
            typeArgs: SynMemberFlags ->
            typeInstantiation: FSharpType list ->
            argTypes: FSharpType list ->
            argExprs: FSharpExpr list ->
                unit

        default WalkTraitCall:
            sourceTypes: FSharpType list ->
            traitName: string ->
            typeArgs: SynMemberFlags ->
            typeInstantiation: FSharpType list ->
            argTypes: FSharpType list ->
            argExprs: FSharpExpr list ->
                unit

        /// Overwriting this member hooks up a custom operation for value sets expressions.
        abstract WalkValueSet: valToSet: FSharpMemberOrFunctionOrValue -> valueExpr: FSharpExpr -> unit
        default WalkValueSet: valToSet: FSharpMemberOrFunctionOrValue -> valueExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for while loops.
        abstract WalkWhileLoop: guardExpr: FSharpExpr -> bodyExpr: FSharpExpr -> unit
        default WalkWhileLoop: guardExpr: FSharpExpr -> bodyExpr: FSharpExpr -> unit

        /// Overwriting this member hooks up a custom operation for base value expressions.
        abstract WalkBaseValue: baseType: FSharpType -> unit
        default WalkBaseValue: baseType: FSharpType -> unit

        /// Overwriting this member hooks up a custom operation for default value expressions.
        abstract WalkDefaultValue: defaultType: FSharpType -> unit
        default WalkDefaultValue: defaultType: FSharpType -> unit

        /// Overwriting this member hooks up a custom operation for this value expressions.
        abstract WalkThisValue: thisType: FSharpType -> unit
        default WalkThisValue: thisType: FSharpType -> unit

        /// Overwriting this member hooks up a custom operation for const value expressions.
        abstract WalkConst: constValueObj: obj -> constType: FSharpType -> unit
        default WalkConst: constValueObj: obj -> constType: FSharpType -> unit

        /// Overwriting this member hooks up a custom operation for value expressions.
        abstract WalkValue: valueToGet: FSharpMemberOrFunctionOrValue -> unit
        default WalkValue: valueToGet: FSharpMemberOrFunctionOrValue -> unit

    /// Traverses the whole TAST and calls the appropriate members of the given TypedTreeCollectorBase
    /// to process the tree elements.
    val walkTast: walker: TypedTreeCollectorBase -> tast: FSharpImplementationFileContents -> unit

    /// Set this to use a custom logger
    val mutable logger: ILogger
