namespace FSharp.Analyzers.SDK.V1

/// A source range decoupled from the FCS range type.
type SourceRange =
    {
        FileName: string
        /// 1-based line number.
        StartLine: int
        /// 0-based column number.
        StartColumn: int
        /// 1-based line number.
        EndLine: int
        /// 0-based column number.
        EndColumn: int
    }

[<RequireQualifiedAccess>]
type Severity =
    | Info
    | Hint
    | Warning
    | Error

type Fix =
    {
        FromRange: SourceRange
        FromText: string
        ToText: string
    }

type Message =
    {
        Type: string
        Message: string
        Code: string
        Severity: Severity
        Range: SourceRange
        Fixes: Fix list
    }

[<RequireQualifiedAccess>]
type MemberKind =
    | ClassConstructor
    | Constructor
    | Member
    | PropertyGet
    | PropertySet
    | PropertyGetSet

type MemberFlags =
    {
        IsInstance: bool
        IsDispatchSlot: bool
        IsOverrideOrExplicitImpl: bool
        IsFinal: bool
        MemberKind: MemberKind
    }

type GenericParameterInfo =
    {
        Name: string
        IsSolveAtCompileTime: bool
        IsCompilerGenerated: bool
        IsMeasure: bool
    }

type AnalyzerIgnoreRange =
    | File
    | Range of commentStart: int * commentEnd: int
    | NextLine of commentLine: int
    | CurrentLine of commentLine: int

type ProjectOptionsInfo =
    {
        ProjectFileName: string
        SourceFiles: string list
        ReferencedProjectsPaths: string list
        OtherOptions: string list
    }

type ParameterInfo =
    {
        Name: string option
        Type: TypeInfo
        IsOptionalArg: bool
    }

and EntityInfo =
    {
        FullName: string
        DisplayName: string
        CompiledName: string
        Namespace: string option
        DeclaringEntity: EntityInfo option
        IsModule: bool
        IsNamespace: bool
        IsUnion: bool
        IsRecord: bool
        IsClass: bool
        IsEnum: bool
        IsInterface: bool
        IsValueType: bool
        IsAbstractClass: bool
        IsFSharpModule: bool
        IsFSharpUnion: bool
        IsFSharpRecord: bool
        IsFSharpExceptionDeclaration: bool
        IsMeasure: bool
        IsDelegate: bool
        IsByRef: bool
        IsAbbreviation: bool
        UnionCases: UnionCaseInfo list
        FSharpFields: FieldInfo list
        MembersFunctionsAndValues: MemberOrFunctionOrValueInfo list
        GenericParameters: GenericParameterInfo list
        BaseType: TypeInfo option
        DeclaredInterfaces: TypeInfo list
        AbbreviatedType: TypeInfo option
    }

and TypeInfo =
    {
        BasicQualifiedName: string
        IsAbbreviation: bool
        IsFunctionType: bool
        IsTupleType: bool
        IsStructTupleType: bool
        IsGenericParameter: bool
        HasTypeDefinition: bool
        GenericArguments: TypeInfo list
        AbbreviatedType: TypeInfo option
        TypeDefinition: EntityInfo option
        GenericParameter: GenericParameterInfo option
    }

and MemberOrFunctionOrValueInfo =
    {
        DisplayName: string
        FullName: string
        CompiledName: string
        IsProperty: bool
        IsMethod: bool
        IsCompilerGenerated: bool
        IsMutable: bool
        IsExtensionMember: bool
        IsActivePattern: bool
        IsConstructor: bool
        IsPropertyGetterMethod: bool
        IsPropertySetterMethod: bool
        IsModuleValueOrMember: bool
        IsValue: bool
        IsMember: bool
        IsInstanceMember: bool
        IsInstanceMemberInCompiledCode: bool
        IsDispatchSlot: bool
        IsOverrideOrExplicitInterfaceImplementation: bool
        DeclaringEntity: EntityInfo option
        FullType: TypeInfo
        CurriedParameterGroups: ParameterInfo list list
        ReturnParameter: ParameterInfo option
        GenericParameters: GenericParameterInfo list
    }

and FieldInfo =
    {
        Name: string
        FieldType: TypeInfo
        IsCompilerGenerated: bool
        IsMutable: bool
        IsStatic: bool
        IsVolatile: bool
        IsLiteral: bool
        LiteralValue: obj option
        DeclaringEntity: EntityInfo option
    }

and UnionCaseInfo =
    {
        Name: string
        CompiledName: string
        Fields: FieldInfo list
        ReturnType: TypeInfo
        DeclaringEntity: EntityInfo option
        HasFields: bool
    }

type ObjectExprOverrideInfo =
    {
        Signature: MemberOrFunctionOrValueInfo
        Body: TypedExpr
        CurriedParameterGroups: MemberOrFunctionOrValueInfo list list
        GenericParameters: GenericParameterInfo list
    }

and TypedExpr =
    | AddressOf of lvalueExpr: TypedExpr
    | AddressSet of lvalueExpr: TypedExpr * rvalueExpr: TypedExpr
    | Application of funcExpr: TypedExpr * typeArgs: TypeInfo list * argExprs: TypedExpr list
    | Call of
        objExpr: TypedExpr option *
        memberOrFunc: MemberOrFunctionOrValueInfo *
        objTypeArgs: TypeInfo list *
        memberTypeArgs: TypeInfo list *
        argExprs: TypedExpr list *
        range: SourceRange
    | Coerce of targetType: TypeInfo * expr: TypedExpr
    | FastIntegerForLoop of start: TypedExpr * limit: TypedExpr * consume: TypedExpr * isUp: bool
    | IfThenElse of guard: TypedExpr * thenExpr: TypedExpr * elseExpr: TypedExpr
    | Lambda of var: MemberOrFunctionOrValueInfo * body: TypedExpr
    | Let of binding: MemberOrFunctionOrValueInfo * bindingExpr: TypedExpr * body: TypedExpr
    | LetRec of bindings: (MemberOrFunctionOrValueInfo * TypedExpr) list * body: TypedExpr
    | NewArray of arrayType: TypeInfo * args: TypedExpr list
    | NewDelegate of delegateType: TypeInfo * body: TypedExpr
    | NewObject of
        ctor: MemberOrFunctionOrValueInfo *
        typeArgs: TypeInfo list *
        args: TypedExpr list
    | NewRecord of recordType: TypeInfo * args: TypedExpr list * range: SourceRange
    | NewTuple of tupleType: TypeInfo * args: TypedExpr list
    | NewUnionCase of unionType: TypeInfo * case: UnionCaseInfo * args: TypedExpr list
    | Quote of expr: TypedExpr
    | FieldGet of objExpr: TypedExpr option * recordOrClassType: TypeInfo * field: FieldInfo
    | FieldSet of
        objExpr: TypedExpr option *
        recordOrClassType: TypeInfo *
        field: FieldInfo *
        value: TypedExpr
    | Sequential of first: TypedExpr * second: TypedExpr
    | TryFinally of body: TypedExpr * finalizer: TypedExpr
    | TryWith of
        body: TypedExpr *
        filterVar: MemberOrFunctionOrValueInfo *
        filterExpr: TypedExpr *
        catchVar: MemberOrFunctionOrValueInfo *
        catchExpr: TypedExpr
    | TupleGet of tupleType: TypeInfo * index: int * tuple: TypedExpr
    | DecisionTree of
        decision: TypedExpr *
        targets: (MemberOrFunctionOrValueInfo list * TypedExpr) list
    | DecisionTreeSuccess of targetIdx: int * targetExprs: TypedExpr list
    | TypeLambda of genericParams: GenericParameterInfo list * body: TypedExpr
    | TypeTest of ty: TypeInfo * expr: TypedExpr
    | UnionCaseSet of
        unionExpr: TypedExpr *
        unionType: TypeInfo *
        case: UnionCaseInfo *
        field: FieldInfo *
        value: TypedExpr
    | UnionCaseGet of
        unionExpr: TypedExpr *
        unionType: TypeInfo *
        case: UnionCaseInfo *
        field: FieldInfo
    | UnionCaseTest of unionExpr: TypedExpr * unionType: TypeInfo * case: UnionCaseInfo
    | UnionCaseTag of unionExpr: TypedExpr * unionType: TypeInfo
    | ObjectExpr of
        objType: TypeInfo *
        baseCall: TypedExpr *
        overrides: ObjectExprOverrideInfo list *
        interfaceImpls: (TypeInfo * ObjectExprOverrideInfo list) list
    | TraitCall of
        sourceTypes: TypeInfo list *
        traitName: string *
        memberFlags: MemberFlags *
        typeInstantiation: TypeInfo list *
        argTypes: TypeInfo list *
        argExprs: TypedExpr list
    | ValueSet of valToSet: MemberOrFunctionOrValueInfo * value: TypedExpr
    | WhileLoop of guard: TypedExpr * body: TypedExpr
    | BaseValue of baseType: TypeInfo
    | DefaultValue of defaultType: TypeInfo
    | ThisValue of thisType: TypeInfo
    | Const of value: obj * constType: TypeInfo
    | Value of valueToGet: MemberOrFunctionOrValueInfo
    | ILAsm of asmCode: string * typeArgs: TypeInfo list * argExprs: TypedExpr list
    | ILFieldGet of objExpr: TypedExpr option * fieldType: TypeInfo * fieldName: string
    | ILFieldSet of
        objExpr: TypedExpr option *
        fieldType: TypeInfo *
        fieldName: string *
        value: TypedExpr
    | Unknown of range: SourceRange

and TypedDeclaration =
    | Entity of entity: EntityInfo * subDeclarations: TypedDeclaration list
    | MemberOrFunctionOrValue of
        value: MemberOrFunctionOrValueInfo *
        curriedArgs: MemberOrFunctionOrValueInfo list list *
        body: TypedExpr
    | InitAction of expr: TypedExpr

[<RequireQualifiedAccess>]
type SymbolInfo =
    | Entity of EntityInfo
    | MemberOrFunctionOrValue of MemberOrFunctionOrValueInfo
    | Field of FieldInfo
    | UnionCase of UnionCaseInfo
    | GenericParameter of GenericParameterInfo
    | Other of displayName: string

type SymbolUseInfo =
    {
        Symbol: SymbolInfo
        Range: SourceRange
        IsFromDefinition: bool
        IsFromPattern: bool
        IsFromType: bool
        IsFromAttribute: bool
        IsFromDispatchSlotImplementation: bool
        IsFromComputationExpression: bool
        IsFromOpenStatement: bool
    }

/// Opaque handle to the FCS typed tree.
/// Pass to TASTCollecting.convertTast to obtain V1 typed declarations.
[<Struct>]
type TypedTreeHandle =
    internal
        {
            Contents: FSharp.Compiler.Symbols.FSharpImplementationFileContents
        }

type CliContext =
    {
        FileName: string
        SourceText: string
        TypedTree: TypedTreeHandle option
        ProjectOptions: ProjectOptionsInfo
        AnalyzerIgnoreRanges: Map<string, AnalyzerIgnoreRange list>
        SymbolUsesInFile: SymbolUseInfo list
        SymbolUsesInProject: SymbolUseInfo list
    }
