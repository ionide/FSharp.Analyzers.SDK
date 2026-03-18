module internal FSharp.Analyzers.SDK.AdapterV1

open System.Collections.Generic
open System.Runtime.CompilerServices
open FSharp.Compiler.Text
open FSharp.Compiler.Symbols
open FSharp.Compiler.Syntax
open FSharp.Compiler.CodeAnalysis
open FSharp.Analyzers.SDK.V1
// FSharpExprPatterns must be opened last so its active patterns
// shadow the identically-named TypedExpr DU cases from V1.
open FSharp.Compiler.Symbols.FSharpExprPatterns

// Cache keyed by logical identity (FullName) rather than reference identity,
// because FCS may return different objects for the same logical entity when
// accessed via different paths (e.g. member.DeclaringEntity vs the entity
// directly).  Only entities are cached (using a stub to break cycles).
// Types are NOT cached because BasicQualifiedName does not distinguish
// generic instantiations (e.g. list<int> vs list<string>).
//
// Entities whose FullName is unavailable (local, anonymous, compiler-generated)
// are cached separately by reference identity so they don't collide.
let private referenceComparer<'T when 'T: not struct> =
    { new IEqualityComparer<'T> with
        member _.Equals(x, y) = obj.ReferenceEquals(x, y)
        member _.GetHashCode(x) = RuntimeHelpers.GetHashCode(x)
    }

type ConversionCache() =
    member val Entities = Dictionary<string, EntityInfo>()
    member val EntitiesByRef = Dictionary<FSharpEntity, EntityInfo>(referenceComparer)

let inline private tryGet defaultValue ([<InlineIfLambda>] f) =
    try
        f ()
    with _ ->
        defaultValue

let private emptyTypeInfo: TypeInfo =
    {
        BasicQualifiedName = ""
        IsAbbreviation = false
        IsFunctionType = false
        IsTupleType = false
        IsStructTupleType = false
        IsGenericParameter = false
        HasTypeDefinition = false
        GenericArguments = []
        AbbreviatedType = None
        TypeDefinition = None
        GenericParameter = None
    }

// ─── Range conversion ───────────────────────────────────────────────

let rangeToV1 (r: range) : SourceRange =
    {
        FileName = r.FileName
        StartLine = r.StartLine
        StartColumn = r.StartColumn
        EndLine = r.EndLine
        EndColumn = r.EndColumn
    }

let rangeFromV1 (sr: SourceRange) : range =
    let start = Position.mkPos sr.StartLine sr.StartColumn
    let finish = Position.mkPos sr.EndLine sr.EndColumn
    Range.mkRange sr.FileName start finish

// ─── Generic parameter conversion ───────────────────────────────────

let genericParameterToV1 (gp: FSharpGenericParameter) : GenericParameterInfo =
    {
        Name = gp.Name
        IsSolveAtCompileTime = gp.IsSolveAtCompileTime
        IsCompilerGenerated = gp.IsCompilerGenerated
        IsMeasure = gp.IsMeasure
    }

// ─── Mutually recursive FCS type conversions ────────────────────────

let rec entityToV1 (cache: ConversionCache) (e: FSharpEntity) : EntityInfo =
    // Use FullName as cache key when available; entities whose FullName
    // throws (local, anonymous, compiler-generated) use a separate
    // reference-identity cache so distinct nameless entities don't collide.
    let fullName = tryGet None (fun () -> Some e.FullName)

    let cached =
        match fullName with
        | Some name ->
            match cache.Entities.TryGetValue(name) with
            | true, v -> ValueSome v
            | _ -> ValueNone
        | None ->
            match cache.EntitiesByRef.TryGetValue(e) with
            | true, v -> ValueSome v
            | _ -> ValueNone

    match cached with
    | ValueSome v -> v
    | ValueNone ->
        let fullNameStr =
            fullName
            |> Option.defaultValue ""

        let cacheSet v =
            match fullName with
            | Some name -> cache.Entities.[name] <- v
            | None -> cache.EntitiesByRef.[e] <- v

        // Insert a stub to break cycles.
        let stub: EntityInfo =
            {
                FullName = fullNameStr
                DisplayName = tryGet "" (fun () -> e.DisplayName)
                CompiledName = tryGet "" (fun () -> e.CompiledName)
                Namespace = tryGet None (fun () -> e.Namespace)
                DeclaringEntity = None
                IsModule = tryGet false (fun () -> e.IsFSharpModule)
                IsNamespace = tryGet false (fun () -> e.IsNamespace)
                IsUnion = tryGet false (fun () -> e.IsFSharpUnion)
                IsRecord = tryGet false (fun () -> e.IsFSharpRecord)
                IsClass = tryGet false (fun () -> e.IsClass)
                IsEnum = tryGet false (fun () -> e.IsEnum)
                IsInterface = tryGet false (fun () -> e.IsInterface)
                IsValueType = tryGet false (fun () -> e.IsValueType)
                IsAbstractClass = tryGet false (fun () -> e.IsAbstractClass)
                IsFSharpModule = tryGet false (fun () -> e.IsFSharpModule)
                IsFSharpUnion = tryGet false (fun () -> e.IsFSharpUnion)
                IsFSharpRecord = tryGet false (fun () -> e.IsFSharpRecord)
                IsFSharpExceptionDeclaration =
                    tryGet false (fun () -> e.IsFSharpExceptionDeclaration)
                IsMeasure = tryGet false (fun () -> e.IsMeasure)
                IsDelegate = tryGet false (fun () -> e.IsDelegate)
                IsByRef = tryGet false (fun () -> e.IsByRef)
                IsAbbreviation = tryGet false (fun () -> e.IsFSharpAbbreviation)
                UnionCases = []
                FSharpFields = []
                MembersFunctionsAndValues = []
                GenericParameters = []
                BaseType = None
                DeclaredInterfaces = []
                AbbreviatedType = None
            }

        cacheSet stub

        let full: EntityInfo =
            { stub with
                DeclaringEntity =
                    tryGet
                        None
                        (fun () ->
                            e.DeclaringEntity
                            |> Option.map (entityToV1 cache)
                        )
                UnionCases =
                    tryGet
                        []
                        (fun () ->
                            e.UnionCases
                            |> Seq.map (unionCaseToV1 cache)
                            |> Seq.toList
                        )
                FSharpFields =
                    tryGet
                        []
                        (fun () ->
                            e.FSharpFields
                            |> Seq.map (fieldToV1 cache)
                            |> Seq.toList
                        )
                MembersFunctionsAndValues =
                    tryGet
                        []
                        (fun () ->
                            e.MembersFunctionsAndValues
                            |> Seq.map (memberToV1 cache)
                            |> Seq.toList
                        )
                GenericParameters =
                    tryGet
                        []
                        (fun () ->
                            e.GenericParameters
                            |> Seq.map genericParameterToV1
                            |> Seq.toList
                        )
                BaseType =
                    tryGet
                        None
                        (fun () ->
                            e.BaseType
                            |> Option.map (typeToV1 cache)
                        )
                DeclaredInterfaces =
                    tryGet
                        []
                        (fun () ->
                            e.DeclaredInterfaces
                            |> Seq.map (typeToV1 cache)
                            |> Seq.toList
                        )
                AbbreviatedType =
                    tryGet
                        None
                        (fun () ->
                            if e.IsFSharpAbbreviation then
                                Some(typeToV1 cache e.AbbreviatedType)
                            else
                                None
                        )
            }

        cacheSet full
        full

and typeToV1 (cache: ConversionCache) (t: FSharpType) : TypeInfo =
    {
        BasicQualifiedName = tryGet "" (fun () -> t.BasicQualifiedName)
        IsAbbreviation = tryGet false (fun () -> t.IsAbbreviation)
        IsFunctionType = tryGet false (fun () -> t.IsFunctionType)
        IsTupleType = tryGet false (fun () -> t.IsTupleType)
        IsStructTupleType = tryGet false (fun () -> t.IsStructTupleType)
        IsGenericParameter = tryGet false (fun () -> t.IsGenericParameter)
        HasTypeDefinition = tryGet false (fun () -> t.HasTypeDefinition)
        GenericArguments =
            tryGet
                []
                (fun () ->
                    t.GenericArguments
                    |> Seq.map (typeToV1 cache)
                    |> Seq.toList
                )
        AbbreviatedType =
            tryGet
                None
                (fun () ->
                    if t.IsAbbreviation then
                        Some(typeToV1 cache t.AbbreviatedType)
                    else
                        None
                )
        TypeDefinition =
            tryGet
                None
                (fun () ->
                    if t.HasTypeDefinition then
                        Some(entityToV1 cache t.TypeDefinition)
                    else
                        None
                )
        GenericParameter =
            tryGet
                None
                (fun () ->
                    if t.IsGenericParameter then
                        Some(genericParameterToV1 t.GenericParameter)
                    else
                        None
                )
    }

and memberToV1
    (cache: ConversionCache)
    (m: FSharpMemberOrFunctionOrValue)
    : MemberOrFunctionOrValueInfo
    =
    {
        DisplayName = tryGet "" (fun () -> m.DisplayName)
        FullName = tryGet "" (fun () -> m.FullName)
        CompiledName = tryGet "" (fun () -> m.CompiledName)
        IsProperty = tryGet false (fun () -> m.IsProperty)
        IsMethod = tryGet false (fun () -> m.IsMethod)
        IsCompilerGenerated = tryGet false (fun () -> m.IsCompilerGenerated)
        IsMutable = tryGet false (fun () -> m.IsMutable)
        IsExtensionMember = tryGet false (fun () -> m.IsExtensionMember)
        IsActivePattern = tryGet false (fun () -> m.IsActivePattern)
        IsConstructor = tryGet false (fun () -> m.IsConstructor)
        IsPropertyGetterMethod = tryGet false (fun () -> m.IsPropertyGetterMethod)
        IsPropertySetterMethod = tryGet false (fun () -> m.IsPropertySetterMethod)
        IsModuleValueOrMember = tryGet false (fun () -> m.IsModuleValueOrMember)
        IsValue = tryGet false (fun () -> m.IsValue)
        IsMember = tryGet false (fun () -> m.IsMember)
        IsInstanceMember = tryGet false (fun () -> m.IsInstanceMember)
        IsInstanceMemberInCompiledCode = tryGet false (fun () -> m.IsInstanceMemberInCompiledCode)
        IsDispatchSlot = tryGet false (fun () -> m.IsDispatchSlot)
        IsOverrideOrExplicitInterfaceImplementation =
            tryGet false (fun () -> m.IsOverrideOrExplicitInterfaceImplementation)
        DeclaringEntity =
            tryGet
                None
                (fun () ->
                    m.DeclaringEntity
                    |> Option.map (entityToV1 cache)
                )
        FullType = tryGet emptyTypeInfo (fun () -> typeToV1 cache m.FullType)
        CurriedParameterGroups =
            tryGet
                []
                (fun () ->
                    m.CurriedParameterGroups
                    |> Seq.map (fun group ->
                        group
                        |> Seq.map (parameterToV1 cache)
                        |> Seq.toList
                    )
                    |> Seq.toList
                )
        ReturnParameter = tryGet None (fun () -> Some(parameterToV1 cache m.ReturnParameter))
        GenericParameters =
            tryGet
                []
                (fun () ->
                    m.GenericParameters
                    |> Seq.map genericParameterToV1
                    |> Seq.toList
                )
    }

and parameterToV1 (cache: ConversionCache) (p: FSharpParameter) : ParameterInfo =
    {
        Name = tryGet None (fun () -> p.Name)
        Type = tryGet emptyTypeInfo (fun () -> typeToV1 cache p.Type)
        IsOptionalArg = tryGet false (fun () -> p.IsOptionalArg)
    }

and abstractParameterToV1 (cache: ConversionCache) (p: FSharpAbstractParameter) : ParameterInfo =
    {
        Name = tryGet None (fun () -> p.Name)
        Type = tryGet emptyTypeInfo (fun () -> typeToV1 cache p.Type)
        IsOptionalArg = tryGet false (fun () -> p.IsOptionalArg)
    }

and fieldToV1 (cache: ConversionCache) (f: FSharpField) : FieldInfo =
    {
        Name = tryGet "" (fun () -> f.Name)
        FieldType = tryGet emptyTypeInfo (fun () -> typeToV1 cache f.FieldType)
        IsCompilerGenerated = tryGet false (fun () -> f.IsCompilerGenerated)
        IsMutable = tryGet false (fun () -> f.IsMutable)
        IsStatic = tryGet false (fun () -> f.IsStatic)
        IsVolatile = tryGet false (fun () -> f.IsVolatile)
        IsLiteral = tryGet false (fun () -> f.IsLiteral)
        LiteralValue = tryGet None (fun () -> f.LiteralValue)
        DeclaringEntity =
            tryGet
                None
                (fun () ->
                    f.DeclaringEntity
                    |> Option.map (entityToV1 cache)
                )
    }

and unionCaseToV1 (cache: ConversionCache) (uc: FSharpUnionCase) : UnionCaseInfo =
    {
        Name = tryGet "" (fun () -> uc.Name)
        CompiledName = tryGet "" (fun () -> uc.CompiledName)
        Fields =
            tryGet
                []
                (fun () ->
                    uc.Fields
                    |> Seq.map (fieldToV1 cache)
                    |> Seq.toList
                )
        ReturnType = tryGet emptyTypeInfo (fun () -> typeToV1 cache uc.ReturnType)
        DeclaringEntity = tryGet None (fun () -> Some(entityToV1 cache uc.DeclaringEntity))
        HasFields = tryGet false (fun () -> uc.HasFields)
    }

// ─── Member flags conversion ────────────────────────────────────────

let memberKindToV1 (mk: SynMemberKind) : MemberKind =
    match mk with
    | SynMemberKind.ClassConstructor -> MemberKind.ClassConstructor
    | SynMemberKind.Constructor -> MemberKind.Constructor
    | SynMemberKind.Member -> MemberKind.Member
    | SynMemberKind.PropertyGet -> MemberKind.PropertyGet
    | SynMemberKind.PropertySet -> MemberKind.PropertySet
    | SynMemberKind.PropertyGetSet -> MemberKind.PropertyGetSet

let memberFlagsToV1 (mf: SynMemberFlags) : MemberFlags =
    {
        IsInstance = mf.IsInstance
        IsDispatchSlot = mf.IsDispatchSlot
        IsOverrideOrExplicitImpl = mf.IsOverrideOrExplicitImpl
        IsFinal = mf.IsFinal
        MemberKind = memberKindToV1 mf.MemberKind
    }

// ─── Symbol use conversion ──────────────────────────────────────────

let symbolToV1 (cache: ConversionCache) (s: FSharpSymbol) : SymbolInfo =
    match s with
    | :? FSharpEntity as e -> SymbolInfo.Entity(entityToV1 cache e)
    | :? FSharpMemberOrFunctionOrValue as m ->
        SymbolInfo.MemberOrFunctionOrValue(memberToV1 cache m)
    | :? FSharpField as f -> SymbolInfo.Field(fieldToV1 cache f)
    | :? FSharpUnionCase as uc -> SymbolInfo.UnionCase(unionCaseToV1 cache uc)
    | :? FSharpGenericParameter as gp -> SymbolInfo.GenericParameter(genericParameterToV1 gp)
    | _ -> SymbolInfo.Other(tryGet "" (fun () -> s.DisplayName))

let symbolUseToV1 (cache: ConversionCache) (su: FSharpSymbolUse) : SymbolUseInfo =
    {
        Symbol = symbolToV1 cache su.Symbol
        Range = rangeToV1 su.Range
        IsFromDefinition = su.IsFromDefinition
        IsFromPattern = su.IsFromPattern
        IsFromType = su.IsFromType
        IsFromAttribute = su.IsFromAttribute
        IsFromDispatchSlotImplementation = su.IsFromDispatchSlotImplementation
        IsFromComputationExpression = su.IsFromComputationExpression
        IsFromOpenStatement = su.IsFromOpenStatement
    }

// ─── ObjectExprOverride conversion ──────────────────────────────────

let objectExprOverrideToV1
    (cache: ConversionCache)
    (exprConvert: ConversionCache -> FSharpExpr -> TypedExpr)
    (o: FSharpObjectExprOverride)
    : ObjectExprOverrideInfo
    =
    let sig' = o.Signature

    let signatureInfo: MemberOrFunctionOrValueInfo =
        {
            DisplayName = tryGet "" (fun () -> sig'.Name)
            FullName = tryGet "" (fun () -> sig'.Name)
            CompiledName = tryGet "" (fun () -> sig'.Name)
            IsProperty = false
            IsMethod = true
            IsCompilerGenerated = false
            IsMutable = false
            IsExtensionMember = false
            IsActivePattern = false
            IsConstructor = false
            IsPropertyGetterMethod = false
            IsPropertySetterMethod = false
            IsModuleValueOrMember = false
            IsValue = false
            IsMember = true
            IsInstanceMember = true
            IsInstanceMemberInCompiledCode = true
            IsDispatchSlot = false
            IsOverrideOrExplicitInterfaceImplementation = true
            DeclaringEntity = None
            FullType = tryGet emptyTypeInfo (fun () -> typeToV1 cache sig'.AbstractReturnType)
            CurriedParameterGroups =
                tryGet
                    []
                    (fun () ->
                        sig'.AbstractArguments
                        |> Seq.map (fun group ->
                            group
                            |> Seq.map (abstractParameterToV1 cache)
                            |> Seq.toList
                        )
                        |> Seq.toList
                    )
            ReturnParameter = None
            GenericParameters =
                tryGet
                    []
                    (fun () ->
                        sig'.MethodGenericParameters
                        |> Seq.map genericParameterToV1
                        |> Seq.toList
                    )
        }

    {
        Signature = signatureInfo
        Body = exprConvert cache o.Body
        CurriedParameterGroups =
            tryGet
                []
                (fun () ->
                    o.CurriedParameterGroups
                    |> Seq.map (fun group ->
                        group
                        |> Seq.map (memberToV1 cache)
                        |> Seq.toList
                    )
                    |> Seq.toList
                )
        GenericParameters =
            tryGet
                []
                (fun () ->
                    o.GenericParameters
                    |> Seq.map genericParameterToV1
                    |> Seq.toList
                )
    }

// ─── Expression conversion ──────────────────────────────────────────
// Mirrors the structure of TASTCollecting.visitExpr but builds TypedExpr values.

let rec exprToV1 (cache: ConversionCache) (e: FSharpExpr) : TypedExpr =
    match e with
    | AddressOf lvalueExpr -> TypedExpr.AddressOf(exprToV1 cache lvalueExpr)
    | AddressSet(lvalueExpr, rvalueExpr) ->
        TypedExpr.AddressSet(exprToV1 cache lvalueExpr, exprToV1 cache rvalueExpr)
    | Application(funcExpr, typeArgs, argExprs) ->
        TypedExpr.Application(
            exprToV1 cache funcExpr,
            typeArgs
            |> List.map (typeToV1 cache),
            argExprs
            |> List.map (exprToV1 cache)
        )
    | Call(objExprOpt, memberOrFunc, objExprTypeArgs, memberOrFuncTypeArgs, argExprs) ->
        TypedExpr.Call(
            objExprOpt
            |> Option.map (exprToV1 cache),
            memberToV1 cache memberOrFunc,
            objExprTypeArgs
            |> List.map (typeToV1 cache),
            memberOrFuncTypeArgs
            |> List.map (typeToV1 cache),
            argExprs
            |> List.map (exprToV1 cache),
            rangeToV1 e.Range
        )
    | Coerce(targetType, inpExpr) ->
        TypedExpr.Coerce(typeToV1 cache targetType, exprToV1 cache inpExpr)
    | FastIntegerForLoop(startExpr,
                         limitExpr,
                         consumeExpr,
                         isUp,
                         _debugPointAtFor,
                         _debugPointAtInOrTo) ->
        TypedExpr.FastIntegerForLoop(
            exprToV1 cache startExpr,
            exprToV1 cache limitExpr,
            exprToV1 cache consumeExpr,
            isUp
        )
    | ILAsm(asmCode, typeArgs, argExprs) ->
        TypedExpr.ILAsm(
            asmCode,
            typeArgs
            |> List.map (typeToV1 cache),
            argExprs
            |> List.map (exprToV1 cache)
        )
    | ILFieldGet(objExprOpt, fieldType, fieldName) ->
        TypedExpr.ILFieldGet(
            objExprOpt
            |> Option.map (exprToV1 cache),
            typeToV1 cache fieldType,
            fieldName
        )
    | ILFieldSet(objExprOpt, fieldType, fieldName, valueExpr) ->
        TypedExpr.ILFieldSet(
            objExprOpt
            |> Option.map (exprToV1 cache),
            typeToV1 cache fieldType,
            fieldName,
            exprToV1 cache valueExpr
        )
    | IfThenElse(guardExpr, thenExpr, elseExpr) ->
        TypedExpr.IfThenElse(
            exprToV1 cache guardExpr,
            exprToV1 cache thenExpr,
            exprToV1 cache elseExpr
        )
    | Lambda(lambdaVar, bodyExpr) ->
        TypedExpr.Lambda(memberToV1 cache lambdaVar, exprToV1 cache bodyExpr)
    | Let((bindingVar, bindingExpr, _debugPointAtBinding), bodyExpr) ->
        TypedExpr.Let(
            memberToV1 cache bindingVar,
            exprToV1 cache bindingExpr,
            exprToV1 cache bodyExpr
        )
    | LetRec(recursiveBindings, bodyExpr) ->
        let bindings =
            recursiveBindings
            |> List.map (fun (mfv, expr, _dp) -> (memberToV1 cache mfv, exprToV1 cache expr))

        TypedExpr.LetRec(bindings, exprToV1 cache bodyExpr)
    | NewArray(arrayType, argExprs) ->
        TypedExpr.NewArray(
            typeToV1 cache arrayType,
            argExprs
            |> List.map (exprToV1 cache)
        )
    | NewDelegate(delegateType, delegateBodyExpr) ->
        TypedExpr.NewDelegate(typeToV1 cache delegateType, exprToV1 cache delegateBodyExpr)
    | NewObject(objType, typeArgs, argExprs) ->
        TypedExpr.NewObject(
            memberToV1 cache objType,
            typeArgs
            |> List.map (typeToV1 cache),
            argExprs
            |> List.map (exprToV1 cache)
        )
    | NewRecord(recordType, argExprs) ->
        TypedExpr.NewRecord(
            typeToV1 cache recordType,
            argExprs
            |> List.map (exprToV1 cache),
            rangeToV1 e.Range
        )
    | NewTuple(tupleType, argExprs) ->
        TypedExpr.NewTuple(
            typeToV1 cache tupleType,
            argExprs
            |> List.map (exprToV1 cache)
        )
    | NewUnionCase(unionType, unionCase, argExprs) ->
        TypedExpr.NewUnionCase(
            typeToV1 cache unionType,
            unionCaseToV1 cache unionCase,
            argExprs
            |> List.map (exprToV1 cache)
        )
    | Quote quotedExpr -> TypedExpr.Quote(exprToV1 cache quotedExpr)
    | FSharpFieldGet(objExprOpt, recordOrClassType, fieldInfo) ->
        TypedExpr.FieldGet(
            objExprOpt
            |> Option.map (exprToV1 cache),
            typeToV1 cache recordOrClassType,
            fieldToV1 cache fieldInfo
        )
    | FSharpFieldSet(objExprOpt, recordOrClassType, fieldInfo, argExpr) ->
        TypedExpr.FieldSet(
            objExprOpt
            |> Option.map (exprToV1 cache),
            typeToV1 cache recordOrClassType,
            fieldToV1 cache fieldInfo,
            exprToV1 cache argExpr
        )
    | Sequential(firstExpr, secondExpr) ->
        TypedExpr.Sequential(exprToV1 cache firstExpr, exprToV1 cache secondExpr)
    | TryFinally(bodyExpr, finalizeExpr, _debugPointAtTry, _debugPointAtFinally) ->
        TypedExpr.TryFinally(exprToV1 cache bodyExpr, exprToV1 cache finalizeExpr)
    | TryWith(bodyExpr,
              filterVar,
              filterExpr,
              catchVar,
              catchExpr,
              _debugPointAtTry,
              _debugPointAtWith) ->
        TypedExpr.TryWith(
            exprToV1 cache bodyExpr,
            memberToV1 cache filterVar,
            exprToV1 cache filterExpr,
            memberToV1 cache catchVar,
            exprToV1 cache catchExpr
        )
    | TupleGet(tupleType, tupleElemIndex, tupleExpr) ->
        TypedExpr.TupleGet(typeToV1 cache tupleType, tupleElemIndex, exprToV1 cache tupleExpr)
    | DecisionTree(decisionExpr, decisionTargets) ->
        let targets =
            decisionTargets
            |> List.map (fun (vars, expr) ->
                (vars
                 |> List.map (memberToV1 cache),
                 exprToV1 cache expr)
            )

        TypedExpr.DecisionTree(exprToV1 cache decisionExpr, targets)
    | DecisionTreeSuccess(decisionTargetIdx, decisionTargetExprs) ->
        TypedExpr.DecisionTreeSuccess(
            decisionTargetIdx,
            decisionTargetExprs
            |> List.map (exprToV1 cache)
        )
    | TypeLambda(genericParam, bodyExpr) ->
        TypedExpr.TypeLambda(
            genericParam
            |> List.map genericParameterToV1,
            exprToV1 cache bodyExpr
        )
    | TypeTest(ty, inpExpr) -> TypedExpr.TypeTest(typeToV1 cache ty, exprToV1 cache inpExpr)
    | UnionCaseSet(unionExpr, unionType, unionCase, unionCaseField, valueExpr) ->
        TypedExpr.UnionCaseSet(
            exprToV1 cache unionExpr,
            typeToV1 cache unionType,
            unionCaseToV1 cache unionCase,
            fieldToV1 cache unionCaseField,
            exprToV1 cache valueExpr
        )
    | UnionCaseGet(unionExpr, unionType, unionCase, unionCaseField) ->
        TypedExpr.UnionCaseGet(
            exprToV1 cache unionExpr,
            typeToV1 cache unionType,
            unionCaseToV1 cache unionCase,
            fieldToV1 cache unionCaseField
        )
    | UnionCaseTest(unionExpr, unionType, unionCase) ->
        TypedExpr.UnionCaseTest(
            exprToV1 cache unionExpr,
            typeToV1 cache unionType,
            unionCaseToV1 cache unionCase
        )
    | UnionCaseTag(unionExpr, unionType) ->
        TypedExpr.UnionCaseTag(exprToV1 cache unionExpr, typeToV1 cache unionType)
    | ObjectExpr(objType, baseCallExpr, overrides, interfaceImplementations) ->
        TypedExpr.ObjectExpr(
            typeToV1 cache objType,
            exprToV1 cache baseCallExpr,
            overrides
            |> List.map (objectExprOverrideToV1 cache exprToV1),
            interfaceImplementations
            |> List.map (fun (ty, impls) ->
                (typeToV1 cache ty,
                 impls
                 |> List.map (objectExprOverrideToV1 cache exprToV1))
            )
        )
    | TraitCall(sourceTypes, traitName, typeArgs, typeInstantiation, argTypes, argExprs) ->
        TypedExpr.TraitCall(
            sourceTypes
            |> List.map (typeToV1 cache),
            traitName,
            memberFlagsToV1 typeArgs,
            typeInstantiation
            |> List.map (typeToV1 cache),
            argTypes
            |> List.map (typeToV1 cache),
            argExprs
            |> List.map (exprToV1 cache)
        )
    | ValueSet(valToSet, valueExpr) ->
        TypedExpr.ValueSet(memberToV1 cache valToSet, exprToV1 cache valueExpr)
    | WhileLoop(guardExpr, bodyExpr, _debugPointAtWhile) ->
        TypedExpr.WhileLoop(exprToV1 cache guardExpr, exprToV1 cache bodyExpr)
    | BaseValue baseType -> TypedExpr.BaseValue(typeToV1 cache baseType)
    | DefaultValue defaultType -> TypedExpr.DefaultValue(typeToV1 cache defaultType)
    | ThisValue thisType -> TypedExpr.ThisValue(typeToV1 cache thisType)
    | Const(constValueObj, constType) -> TypedExpr.Const(constValueObj, typeToV1 cache constType)
    | Value valueToGet -> TypedExpr.Value(memberToV1 cache valueToGet)
    | _ -> TypedExpr.Unknown(rangeToV1 e.Range)

// ─── Declaration conversion ─────────────────────────────────────────

// FCS throws on certain compiler-generated members (e.g. CompareTo, GetHashCode, Equals)
// whose expression body can't be safely decompiled. These sets mirror the workaround in
// TASTCollecting.visitDeclaration — see:
// https://github.com/dotnet/fsharp/blob/91ff67b5f698f1929f75e65918e998a2df1c1858/src/Compiler/Symbols/Exprs.fs#L1269
let private membersToIgnore =
    set
        [
            "CompareTo"
            "GetHashCode"
            "Equals"
        ]

let private exprTypesToIgnore =
    set
        [
            "Microsoft.FSharp.Core.int"
            "Microsoft.FSharp.Core.bool"
        ]

let private shouldSkipMember (v: FSharpMemberOrFunctionOrValue) (e: FSharpExpr) =
    v.IsCompilerGenerated
    && Set.contains v.CompiledName membersToIgnore
    && e.Type.IsAbbreviation
    && Set.contains e.Type.BasicQualifiedName exprTypesToIgnore

let rec declarationToV1
    (cache: ConversionCache)
    (d: FSharpImplementationFileDeclaration)
    : TypedDeclaration option
    =
    match d with
    | FSharpImplementationFileDeclaration.Entity(e, subDecls) ->
        TypedDeclaration.Entity(
            entityToV1 cache e,
            subDecls
            |> List.choose (declarationToV1 cache)
        )
        |> Some
    | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, vs, e) ->
        if shouldSkipMember v e then
            None
        else
            // FCS may throw when decompiling certain expression trees — see:
            // https://github.com/dotnet/fsharp/blob/91ff67b5f698f1929f75e65918e998a2df1c1858/src/Compiler/Symbols/Exprs.fs#L1329
            let body =
                try
                    exprToV1 cache e
                with _ ->
                    TypedExpr.Unknown(rangeToV1 e.Range)

            TypedDeclaration.MemberOrFunctionOrValue(
                memberToV1 cache v,
                vs
                |> List.map (List.map (memberToV1 cache)),
                body
            )
            |> Some
    | FSharpImplementationFileDeclaration.InitAction e ->
        TypedDeclaration.InitAction(exprToV1 cache e)
        |> Some

// ─── Severity / Fix / Message conversion (V1 -> SDK) ────────────────

let severityFromV1 (s: Severity) : FSharp.Analyzers.SDK.Severity =
    match s with
    | Severity.Info -> FSharp.Analyzers.SDK.Severity.Info
    | Severity.Hint -> FSharp.Analyzers.SDK.Severity.Hint
    | Severity.Warning -> FSharp.Analyzers.SDK.Severity.Warning
    | Severity.Error -> FSharp.Analyzers.SDK.Severity.Error

let fixFromV1 (f: Fix) : FSharp.Analyzers.SDK.Fix =
    {
        FromRange = rangeFromV1 f.FromRange
        FromText = f.FromText
        ToText = f.ToText
    }

let messageFromV1 (m: Message) : FSharp.Analyzers.SDK.Message =
    {
        Type = m.Type
        Message = m.Message
        Code = m.Code
        Severity = severityFromV1 m.Severity
        Range = rangeFromV1 m.Range
        Fixes =
            m.Fixes
            |> List.map fixFromV1
    }

// ─── Ignore range conversion ────────────────────────────────────────

let analyzerIgnoreRangeToV1 (r: FSharp.Analyzers.SDK.AnalyzerIgnoreRange) : AnalyzerIgnoreRange =
    match r with
    | FSharp.Analyzers.SDK.AnalyzerIgnoreRange.File -> AnalyzerIgnoreRange.File
    | FSharp.Analyzers.SDK.AnalyzerIgnoreRange.Range(s, e) -> AnalyzerIgnoreRange.Range(s, e)
    | FSharp.Analyzers.SDK.AnalyzerIgnoreRange.NextLine l -> AnalyzerIgnoreRange.NextLine l
    | FSharp.Analyzers.SDK.AnalyzerIgnoreRange.CurrentLine l -> AnalyzerIgnoreRange.CurrentLine l

// ─── Context conversion ─────────────────────────────────────────────

let contextToV1 (ctx: FSharp.Analyzers.SDK.CliContext) : CliContext =
    let cache = ConversionCache()

    {
        FileName = ctx.FileName
        SourceText = ctx.SourceText.GetSubTextString(0, ctx.SourceText.Length)
        TypedTree =
            ctx.TypedTree
            |> Option.map TypedTreeHandle
        ProjectOptions =
            {
                ProjectFileName = ctx.ProjectOptions.ProjectFileName
                SourceFiles = ctx.ProjectOptions.SourceFiles
                ReferencedProjectsPaths = ctx.ProjectOptions.ReferencedProjectsPath
                OtherOptions = ctx.ProjectOptions.OtherOptions
            }
        AnalyzerIgnoreRanges =
            ctx.AnalyzerIgnoreRanges
            |> Map.map (fun _ ranges ->
                ranges
                |> List.map analyzerIgnoreRangeToV1
            )
        SymbolUsesInFile =
            tryGet
                []
                (fun () ->
                    ctx.GetAllSymbolUsesOfFile()
                    |> Seq.map (symbolUseToV1 cache)
                    |> Seq.toList
                )
        SymbolUsesInProject =
            tryGet
                []
                (fun () ->
                    ctx.GetAllSymbolUsesOfProject()
                    |> Seq.map (symbolUseToV1 cache)
                    |> Seq.toList
                )
    }
