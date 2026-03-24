module OptionAnalyzer.V1Tests

#nowarn "57"

open System.Collections.Generic
open System.Runtime.CompilerServices
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open NUnit.Framework
open FsCheck.NUnit
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.Testing
open FSharp.Analyzers.SDK.AdapterV1
open OptionAnalyzer.TestHelpers

// ─── Oracle tests: V1 and legacy analyzers must agree ──────────────

module OracleTests =

    let mutable projectOptions: FSharpProjectOptions = Unchecked.defaultof<_>

    [<OneTimeSetUp>]
    let Setup () =
        task {
            let! opts = mkTestProjectOptions ()
            projectOptions <- opts
        }

    [<Test>]
    let ``V1 and legacy agree on single Option.Value`` () =
        async {
            let source =
                """
module M

let f () =
    let option : Option<int> = None
    option.Value
    """

            let ctx = getContext projectOptions source
            let! legacyMsgs = OptionAnalyzer.optionValueAnalyzer ctx

            let v1Ctx = contextToV1 ctx
            let! v1Raw = OptionAnalyzer.V1.optionValueAnalyzer v1Ctx

            let v1Msgs =
                v1Raw
                |> List.map messageFromV1

            Assert.AreEqual(legacyMsgs.Length, v1Msgs.Length, "message count")

            for legacy, v1 in List.zip legacyMsgs v1Msgs do
                Assert.AreEqual(legacy, v1)
        }

    [<Test>]
    let ``V1 and legacy agree on multiple Option.Value usages`` () =
        async {
            let source =
                """
module M

let f () =
    let a : Option<int> = None
    let b : Option<string> = None
    let _ = a.Value
    let _ = b.Value
    a.Value + b.Value.Length
    """

            let ctx = getContext projectOptions source
            let! legacyMsgs = OptionAnalyzer.optionValueAnalyzer ctx

            let v1Ctx = contextToV1 ctx
            let! v1Raw = OptionAnalyzer.V1.optionValueAnalyzer v1Ctx

            let v1Msgs =
                v1Raw
                |> List.map messageFromV1

            let sort msgs =
                msgs
                |> List.sortBy (fun (m: Message) -> m.Range.StartLine, m.Range.StartColumn)

            Assert.AreEqual(legacyMsgs.Length, v1Msgs.Length, "message count")

            for legacy, v1 in List.zip (sort legacyMsgs) (sort v1Msgs) do
                Assert.AreEqual(legacy, v1)
        }

    [<Test>]
    let ``V1 and legacy agree on clean input`` () =
        async {
            let source =
                """
module M

let f () =
    let x = Some 42
    match x with
    | Some v -> v
    | None -> 0
    """

            let ctx = getContext projectOptions source
            let! legacyMsgs = OptionAnalyzer.optionValueAnalyzer ctx

            let v1Ctx = contextToV1 ctx
            let! v1Raw = OptionAnalyzer.V1.optionValueAnalyzer v1Ctx

            let v1Msgs =
                v1Raw
                |> List.map messageFromV1

            Assert.IsEmpty legacyMsgs
            Assert.IsEmpty v1Msgs
        }

// ─── Client integration tests ──────────────────────────────────────

module ClientIntegrationTests =

    let mutable projectOptions: FSharpProjectOptions = Unchecked.defaultof<_>

    [<OneTimeSetUp>]
    let Setup () =
        task {
            let! opts = mkTestProjectOptions ()
            projectOptions <- opts
        }

    [<Test>]
    let ``LoadAnalyzers includes V1 analyzers`` () =
        let _client, stats = loadAnalyzers ()
        Assert.That(stats.AnalyzerNames, Does.Contain "OptionAnalyzer")
        Assert.That(stats.AnalyzerNames, Does.Contain "InferredReturnAnalyzer")

    [<Test>]
    let ``RunAnalyzersSafely produces results from both legacy and V1`` () =
        async {
            let ctx = getContext projectOptions ClientTestSources.optionValue
            let client, _stats = loadAnalyzers ()
            let! results = client.RunAnalyzersSafely(ctx)

            let optionResults =
                results
                |> List.filter (fun r -> r.AnalyzerName = "OptionAnalyzer")
                |> List.choose (fun r ->
                    match r.Output with
                    | Ok msgs -> Some msgs
                    | Error ex ->
                        Assert.Fail($"Analyzer result was Error: %A{ex}")
                        None
                )

            // Both legacy and V1 OptionAnalyzer should produce results
            Assert.That(
                optionResults
                |> List.filter (fun msgs ->
                    msgs
                    |> List.exists (fun m -> m.Code = "OV001")
                )
                |> List.length,
                Is.GreaterThanOrEqualTo 2,
                "Expected OV001 results from both legacy and V1 OptionAnalyzer"
            )

            for msgs in optionResults do
                for msg in msgs do
                    Assert.AreEqual("OV001", msg.Code)
                    Assert.AreEqual(Severity.Warning, msg.Severity)
        }

    [<Test>]
    let ``LoadAnalyzers discovers V1 analyzer with inferred return type`` () =
        let _client, stats = loadAnalyzers ()

        Assert.That(
            stats.AnalyzerNames,
            Does.Contain "InferredReturnAnalyzer",
            "V1 analyzer with inferred Async<'a list> return type should be discovered"
        )

    [<Test>]
    let ``LoadAnalyzers discovers V1 analyzer with explicit generic parameter`` () =
        let _client, stats = loadAnalyzers ()

        Assert.That(
            stats.AnalyzerNames,
            Does.Contain "ExplicitGenericAnalyzer",
            "V1 analyzer with explicit generic parameter should be discovered"
        )

    [<Test>]
    let ``InferredReturnAnalyzer runs without error`` () =
        async {
            let ctx = getContext projectOptions ClientTestSources.optionValue
            let client, _stats = loadAnalyzers ()
            let! results = client.RunAnalyzersSafely(ctx)

            let inferredResults =
                results
                |> List.filter (fun r -> r.AnalyzerName = "InferredReturnAnalyzer")

            Assert.That(
                inferredResults,
                Is.Not.Empty,
                "InferredReturnAnalyzer should produce a result"
            )

            for r in inferredResults do
                match r.Output with
                | Ok _ -> ()
                | Error ex -> Assert.Fail($"InferredReturnAnalyzer returned Error: %A{ex}")
        }

    [<Test>]
    let ``ExplicitGenericAnalyzer runs without error`` () =
        async {
            let ctx = getContext projectOptions ClientTestSources.optionValue
            let client, _stats = loadAnalyzers ()
            let! results = client.RunAnalyzersSafely(ctx)

            let explicitResults =
                results
                |> List.filter (fun r -> r.AnalyzerName = "ExplicitGenericAnalyzer")

            Assert.That(
                explicitResults,
                Is.Not.Empty,
                "ExplicitGenericAnalyzer should produce a result"
            )

            for r in explicitResults do
                match r.Output with
                | Ok _ -> ()
                | Error ex -> Assert.Fail($"ExplicitGenericAnalyzer returned Error: %A{ex}")
        }

// ─── Adapter unit tests ────────────────────────────────────────────

module AdapterTests =

    // Abbreviations for V1 types to avoid ambiguity with SDK types.
    type V1Severity = FSharp.Analyzers.SDK.V1.Severity
    type V1SourceRange = FSharp.Analyzers.SDK.V1.SourceRange
    type V1Fix = FSharp.Analyzers.SDK.V1.Fix
    type V1Message = FSharp.Analyzers.SDK.V1.Message
    type V1AnalyzerIgnoreRange = FSharp.Analyzers.SDK.V1.AnalyzerIgnoreRange

    let mutable projectOptions: FSharpProjectOptions = Unchecked.defaultof<_>

    [<OneTimeSetUp>]
    let Setup () =
        task {
            let! opts = mkTestProjectOptions ()
            projectOptions <- opts
        }

    [<Property>]
    let ``rangeToV1 then rangeFromV1 preserves all fields``
        (sl: int)
        (sc: int)
        (ls: int)
        (ec: int)
        =
        // Constrain to valid Position.mkPos inputs (line >= 1, col >= 0).
        // Mask sign bit to avoid abs(Int32.MinValue) overflow.
        let startLine =
            (sl
             &&& 0x7FFFFFFF) % 10000
            + 1

        let startCol =
            (sc
             &&& 0x7FFFFFFF) % 200

        let endLine =
            startLine
            + (ls
               &&& 0x7FFFFFFF) % 100

        let endCol =
            (ec
             &&& 0x7FFFFFFF) % 200

        let r =
            Range.mkRange
                "Test.fs"
                (Position.mkPos startLine startCol)
                (Position.mkPos endLine endCol)

        let rt = rangeFromV1 (rangeToV1 r)

        rt.FileName = r.FileName
        && rt.StartLine = r.StartLine
        && rt.StartColumn = r.StartColumn
        && rt.EndLine = r.EndLine
        && rt.EndColumn = r.EndColumn

    [<Test>]
    let ``severityFromV1 maps all cases correctly`` () =
        Assert.AreEqual(Severity.Info, severityFromV1 V1Severity.Info)
        Assert.AreEqual(Severity.Hint, severityFromV1 V1Severity.Hint)
        Assert.AreEqual(Severity.Warning, severityFromV1 V1Severity.Warning)
        Assert.AreEqual(Severity.Error, severityFromV1 V1Severity.Error)

    [<Test>]
    let ``messageFromV1 preserves all fields`` () =
        let v1Range: V1SourceRange =
            {
                FileName = "Test.fs"
                StartLine = 10
                StartColumn = 4
                EndLine = 10
                EndColumn = 16
            }

        let v1Fix: V1Fix =
            {
                FromRange = v1Range
                FromText = "old"
                ToText = "new"
            }

        let v1Msg: V1Message =
            {
                Type = "TestType"
                Message = "TestMessage"
                Code = "T001"
                Severity = V1Severity.Error
                Range = v1Range
                Fixes = [ v1Fix ]
            }

        let sdkMsg = messageFromV1 v1Msg

        Assert.AreEqual("TestType", sdkMsg.Type)
        Assert.AreEqual("TestMessage", sdkMsg.Message)
        Assert.AreEqual("T001", sdkMsg.Code)
        Assert.AreEqual(Severity.Error, sdkMsg.Severity)
        Assert.AreEqual("Test.fs", sdkMsg.Range.FileName)
        Assert.AreEqual(10, sdkMsg.Range.StartLine)
        Assert.AreEqual(4, sdkMsg.Range.StartColumn)
        Assert.AreEqual(10, sdkMsg.Range.EndLine)
        Assert.AreEqual(16, sdkMsg.Range.EndColumn)
        Assert.AreEqual(1, sdkMsg.Fixes.Length)
        Assert.AreEqual("old", sdkMsg.Fixes[0].FromText)
        Assert.AreEqual("new", sdkMsg.Fixes[0].ToText)
        Assert.AreEqual(10, sdkMsg.Fixes[0].FromRange.StartLine)

    [<Test>]
    let ``analyzerIgnoreRangeToV1 maps all cases`` () =
        Assert.AreEqual(
            V1AnalyzerIgnoreRange.File,
            analyzerIgnoreRangeToV1 AnalyzerIgnoreRange.File
        )

        Assert.AreEqual(
            V1AnalyzerIgnoreRange.Range(3, 7),
            analyzerIgnoreRangeToV1 (AnalyzerIgnoreRange.Range(3, 7))
        )

        Assert.AreEqual(
            V1AnalyzerIgnoreRange.NextLine 5,
            analyzerIgnoreRangeToV1 (AnalyzerIgnoreRange.NextLine 5)
        )

        Assert.AreEqual(
            V1AnalyzerIgnoreRange.CurrentLine 10,
            analyzerIgnoreRangeToV1 (AnalyzerIgnoreRange.CurrentLine 10)
        )

    [<Test>]
    let ``contextToV1 preserves filename and source text`` () =
        let source =
            """
module M
let x = 1
"""

        let ctx = getContext projectOptions source
        let v1Ctx = contextToV1 ctx

        Assert.AreEqual(ctx.FileName, v1Ctx.FileName)

        let expectedText = ctx.SourceText.GetSubTextString(0, ctx.SourceText.Length)
        Assert.AreEqual(expectedText, v1Ctx.SourceText)

    [<Test>]
    let ``contextToV1 preserves project options`` () =
        let source =
            """
module M
let x = 1
"""

        let ctx = getContext projectOptions source
        let v1Ctx = contextToV1 ctx

        Assert.AreEqual(ctx.ProjectOptions.ProjectFileName, v1Ctx.ProjectOptions.ProjectFileName)

        Assert.AreEqual(ctx.ProjectOptions.SourceFiles, v1Ctx.ProjectOptions.SourceFiles)

        Assert.AreEqual(ctx.ProjectOptions.OtherOptions, v1Ctx.ProjectOptions.OtherOptions)

    // ─── Helpers for typed-tree inspection ─────────────────────────────

    /// Extract all top-level MemberOrFunctionOrValue bindings from declarations,
    /// descending one level into Entity (module) wrappers.
    let private findBindings (decls: FSharp.Analyzers.SDK.V1.TypedDeclaration list) =
        decls
        |> List.collect (fun d ->
            match d with
            | FSharp.Analyzers.SDK.V1.TypedDeclaration.Entity(_, subDecls) -> subDecls
            | other -> [ other ]
        )
        |> List.choose (fun d ->
            match d with
            | FSharp.Analyzers.SDK.V1.TypedDeclaration.MemberOrFunctionOrValue(v, _, _) -> Some v
            | _ -> None
        )

    let private getBinding name decls =
        findBindings decls
        |> List.find (fun v -> v.DisplayName = name)

    let private convertSource source =
        let ctx = getContext projectOptions source
        let v1Ctx = contextToV1 ctx
        let handle = v1Ctx.TypedTree.Value
        FSharp.Analyzers.SDK.V1.TASTCollecting.convertTast handle

    // ─── TypedTreeHandle opacity tests ─────────────────────────────────

    /// Recursively check whether a System.Type references any type from the
    /// FSharp.Compiler assembly (FCS), including through generic arguments.
    let rec private referencesFCS (ty: System.Type) =
        if
            isNull ty
            || isNull ty.FullName
        then
            false
        elif ty.FullName.StartsWith("FSharp.Compiler", System.StringComparison.Ordinal) then
            true
        elif ty.IsGenericType then
            ty.GetGenericArguments()
            |> Array.exists referencesFCS
        elif ty.IsArray then
            referencesFCS (ty.GetElementType())
        elif ty.IsByRef then
            referencesFCS (ty.GetElementType())
        else
            false

    [<Test>]
    let ``no V1 public type exposes FCS types in its public surface`` () =
        let v1Assembly = typeof<FSharp.Analyzers.SDK.V1.CliContext>.Assembly

        let v1Types =
            v1Assembly.GetTypes()
            |> Array.filter (fun t ->
                t.IsPublic
                && not (isNull t.Namespace)
                && t.Namespace.StartsWith(
                    "FSharp.Analyzers.SDK.V1",
                    System.StringComparison.Ordinal
                )
            )

        // Sanity: we should find a reasonable number of V1 types.
        NUnit.Framework.Assert.That(
            v1Types.Length,
            Is.GreaterThanOrEqualTo(10),
            "Should find V1 types"
        )

        for t in v1Types do
            let publicMembers =
                t.GetMembers(
                    System.Reflection.BindingFlags.Public
                    ||| System.Reflection.BindingFlags.Instance
                    ||| System.Reflection.BindingFlags.Static
                    ||| System.Reflection.BindingFlags.DeclaredOnly
                )

            for m in publicMembers do
                let memberTypes =
                    match m with
                    | :? System.Reflection.PropertyInfo as p -> [ p.PropertyType ]
                    | :? System.Reflection.FieldInfo as f -> [ f.FieldType ]
                    | :? System.Reflection.MethodInfo as mi ->
                        mi.ReturnType
                        :: (mi.GetParameters()
                            |> Array.toList
                            |> List.map (fun p -> p.ParameterType))
                    | :? System.Reflection.ConstructorInfo as ci ->
                        ci.GetParameters()
                        |> Array.toList
                        |> List.map (fun p -> p.ParameterType)
                    | _ -> []

                for mt in memberTypes do
                    NUnit.Framework.Assert.That(
                        referencesFCS mt,
                        Is.False,
                        $"Type '{t.Name}', member '{m.Name}' exposes FCS type '{mt.FullName}'"
                    )

    // ─── Type conversion cache-collision tests ─────────────────────────

    [<Test>]
    let ``typeToV1 distinguishes list<int> from list<string>`` () =
        let source =
            """
module M

let a : list<int> = []
let b : list<string> = []
"""

        let decls = convertSource source
        let aInfo = getBinding "a" decls
        let bInfo = getBinding "b" decls

        // Both should have the same base list type
        Assert.AreEqual(
            aInfo.FullType.BasicQualifiedName,
            bInfo.FullType.BasicQualifiedName,
            "Both should share the same base list type name"
        )

        // But their generic arguments must differ
        Assert.AreEqual(1, aInfo.FullType.GenericArguments.Length, "list<int> has 1 generic arg")
        Assert.AreEqual(1, bInfo.FullType.GenericArguments.Length, "list<string> has 1 generic arg")

        Assert.AreNotEqual(
            aInfo.FullType.GenericArguments.[0].BasicQualifiedName,
            bInfo.FullType.GenericArguments.[0].BasicQualifiedName,
            "list<int> and list<string> must have different generic arguments"
        )

    [<Test>]
    let ``typeToV1 preserves generic argument order for Result<int,string> vs Result<string,int>``
        ()
        =
        let source =
            """
module M

let a : Result<int, string> = Ok 1
let b : Result<string, int> = Ok ""
"""

        let decls = convertSource source
        let aInfo = getBinding "a" decls
        let bInfo = getBinding "b" decls

        // Same base type
        Assert.AreEqual(
            aInfo.FullType.BasicQualifiedName,
            bInfo.FullType.BasicQualifiedName,
            "Both should share the same base Result type name"
        )

        Assert.AreEqual(2, aInfo.FullType.GenericArguments.Length)
        Assert.AreEqual(2, bInfo.FullType.GenericArguments.Length)

        // a's first arg (int) should match b's second arg (int), and vice versa
        Assert.AreEqual(
            aInfo.FullType.GenericArguments.[0].BasicQualifiedName,
            bInfo.FullType.GenericArguments.[1].BasicQualifiedName,
            "First arg of Result<int,string> should equal second arg of Result<string,int>"
        )

        Assert.AreEqual(
            aInfo.FullType.GenericArguments.[1].BasicQualifiedName,
            bInfo.FullType.GenericArguments.[0].BasicQualifiedName,
            "Second arg of Result<int,string> should equal first arg of Result<string,int>"
        )

    [<Test>]
    let ``typeToV1 distinguishes nested generic instantiations`` () =
        let source =
            """
module M

let a : list<list<int>> = []
let b : list<list<string>> = []
"""

        let decls = convertSource source
        let aInfo = getBinding "a" decls
        let bInfo = getBinding "b" decls

        // Outer list types have same BasicQualifiedName
        Assert.AreEqual(aInfo.FullType.BasicQualifiedName, bInfo.FullType.BasicQualifiedName)

        // The inner list's generic argument must differ
        let aInner = aInfo.FullType.GenericArguments.[0]
        let bInner = bInfo.FullType.GenericArguments.[0]

        Assert.AreEqual(
            aInner.BasicQualifiedName,
            bInner.BasicQualifiedName,
            "Inner lists share the same base type name"
        )

        Assert.AreNotEqual(
            aInner.GenericArguments.[0].BasicQualifiedName,
            bInner.GenericArguments.[0].BasicQualifiedName,
            "list<list<int>> and list<list<string>> must differ at the innermost generic argument"
        )

    [<Test>]
    let ``typeToV1 correctly converts two values with the same generic instantiation`` () =
        let source =
            """
module M

let a : list<int> = []
let b : list<int> = []
"""

        let decls = convertSource source
        let aInfo = getBinding "a" decls
        let bInfo = getBinding "b" decls

        // Both should be structurally equal
        Assert.AreEqual(aInfo.FullType.BasicQualifiedName, bInfo.FullType.BasicQualifiedName)

        Assert.AreEqual(
            aInfo.FullType.GenericArguments.[0].BasicQualifiedName,
            bInfo.FullType.GenericArguments.[0].BasicQualifiedName,
            "Two list<int> values should have the same generic argument"
        )

// ─── Cycle diagnostic tests ──────────────────────────────────────

/// Simulate entityToV1's cache and traversal against the live FCS
/// entity/type graph to find what causes unbounded recursion.
module CycleDiagnosticTests =

    let mutable projectOptions: FSharpProjectOptions = Unchecked.defaultof<_>

    [<OneTimeSetUp>]
    let Setup () =
        task {
            let! opts = mkTestProjectOptions ()
            projectOptions <- opts
        }

    let private refEq<'T when 'T: not struct> =
        { new IEqualityComparer<'T> with
            member _.Equals(x, y) = obj.ReferenceEquals(x, y)
            member _.GetHashCode(x) = RuntimeHelpers.GetHashCode(x)
        }

    let inline private safe def ([<InlineIfLambda>] f) =
        try
            f ()
        with _ ->
            def

    /// Walk the FCS entity/type graph following entityToV1's exact
    /// cache logic and field-evaluation order.  Returns diagnostics
    /// instead of crashing.
    type WalkStats =
        {
            CallCount: int
            MaxDepth: int
            EntitiesByName: int
            EntitiesByRef: int
            NoNameEntityEncounters: int
            /// Entities where FullName throws AND FCS returned a
            /// *different* object reference for the same DisplayName.
            DifferentRefNoNameEntities: (string * int) list
            /// True when CallCount hit the safety limit.
            HitLimit: bool
        }

    let walkFrom (symbols: FSharpSymbolUse seq) : WalkStats =
        // Simulate entityToV1's two-tier cache.
        let nameCache = HashSet<string>()
        let refCache = HashSet<FSharpEntity>(refEq)
        let noNameByDisplay = Dictionary<string, FSharpEntity list>()

        let mutable callCount = 0
        let mutable maxDepth = 0
        let mutable noNameHits = 0
        let limit = 1_000_000
        let depthLimit = 400

        let rec walkEntity depth (e: FSharpEntity) =
            callCount <-
                callCount
                + 1

            if depth > maxDepth then
                maxDepth <- depth

            if
                callCount
                >= limit
                || depth
                   >= depthLimit
            then
                ()
            else
                let fn = safe None (fun () -> Some e.FullName)

                // Mirror the three-tier cache key from entityToV1:
                // FullName -> AccessPath.DisplayName -> reference identity
                let stableName =
                    match fn with
                    | Some _ -> fn
                    | None ->
                        safe
                            None
                            (fun () ->
                                let dn = e.DisplayName

                                if System.String.IsNullOrEmpty(dn) then
                                    None
                                else
                                    let ap = e.AccessPath

                                    if System.String.IsNullOrEmpty(ap) then
                                        None
                                    else
                                        Some(
                                            ap
                                            + "."
                                            + dn
                                        )
                            )

                let isHit =
                    match stableName with
                    | Some name -> not (nameCache.Add(name))
                    | None -> not (refCache.Add(e))

                if stableName.IsNone then
                    noNameHits <-
                        noNameHits
                        + 1

                    let dn = safe "???" (fun () -> e.DisplayName)

                    let existing =
                        match noNameByDisplay.TryGetValue(dn) with
                        | true, xs -> xs
                        | _ -> []

                    let hasDiffRef =
                        existing
                        |> List.exists (fun x -> not (obj.ReferenceEquals(x, e)))

                    if hasDiffRef then
                        printfn
                            "CYCLE PROOF: no-name entity '%s' at depth %d — FullName throws AND different FCS object reference (ref-cache miss)"
                            dn
                            depth

                    noNameByDisplay.[dn] <-
                        e
                        :: existing

                if not isHit then
                    let dn = safe "???" (fun () -> e.DisplayName)

                    if fn.IsNone then
                        printfn "  [depth %d] processing NO-NAME entity '%s'" depth dn

                    // 1. DeclaringEntity
                    safe
                        ()
                        (fun () ->
                            e.DeclaringEntity
                            |> Option.iter (walkEntity (depth + 1))
                        )

                    // 2. UnionCases
                    safe
                        ()
                        (fun () ->
                            for uc in e.UnionCases do
                                safe
                                    ()
                                    (fun () ->
                                        for f in uc.Fields do
                                            safe () (fun () -> walkType (depth + 1) f.FieldType)

                                            safe
                                                ()
                                                (fun () ->
                                                    f.DeclaringEntity
                                                    |> Option.iter (walkEntity (depth + 1))
                                                )
                                    )

                                safe () (fun () -> walkType (depth + 1) uc.ReturnType)
                                safe () (fun () -> walkEntity (depth + 1) uc.DeclaringEntity)
                        )

                    // 3. FSharpFields
                    safe
                        ()
                        (fun () ->
                            for f in e.FSharpFields do
                                safe () (fun () -> walkType (depth + 1) f.FieldType)

                                safe
                                    ()
                                    (fun () ->
                                        f.DeclaringEntity
                                        |> Option.iter (walkEntity (depth + 1))
                                    )
                        )

                    // 4. MembersFunctionsAndValues
                    safe
                        ()
                        (fun () ->
                            for m in e.MembersFunctionsAndValues do
                                walkMember (depth + 1) m
                        )

                    // 5. BaseType
                    safe
                        ()
                        (fun () ->
                            e.BaseType
                            |> Option.iter (walkType (depth + 1))
                        )

                    // 6. DeclaredInterfaces
                    safe
                        ()
                        (fun () ->
                            for iface in e.DeclaredInterfaces do
                                walkType (depth + 1) iface
                        )

                    // 7. AbbreviatedType
                    safe
                        ()
                        (fun () ->
                            if e.IsFSharpAbbreviation then
                                walkType (depth + 1) e.AbbreviatedType
                        )

        and walkMember depth (m: FSharpMemberOrFunctionOrValue) =
            if
                depth
                >= depthLimit
            then
                ()
            else

            safe
                ()
                (fun () ->
                    m.DeclaringEntity
                    |> Option.iter (walkEntity (depth + 1))
                )

            safe () (fun () -> walkType (depth + 1) m.FullType)

            safe
                ()
                (fun () ->
                    for group in m.CurriedParameterGroups do
                        for p in group do
                            safe () (fun () -> walkType (depth + 1) p.Type)
                )

            safe () (fun () -> walkType (depth + 1) m.ReturnParameter.Type)

        and walkType depth (t: FSharpType) =
            if
                depth
                >= depthLimit
            then
                ()
            else

            safe
                ()
                (fun () ->
                    for ga in t.GenericArguments do
                        walkType (depth + 1) ga
                )

            safe
                ()
                (fun () ->
                    if t.IsAbbreviation then
                        walkType (depth + 1) t.AbbreviatedType
                )

            safe
                ()
                (fun () ->
                    if t.HasTypeDefinition then
                        walkEntity (depth + 1) t.TypeDefinition
                )

        for su in symbols do
            match su.Symbol with
            | :? FSharpEntity as e -> walkEntity 0 e
            | :? FSharpMemberOrFunctionOrValue as m ->
                safe
                    ()
                    (fun () ->
                        m.DeclaringEntity
                        |> Option.iter (walkEntity 0)
                    )

                safe () (fun () -> walkType 0 m.FullType)
            | :? FSharpField as f ->
                safe () (fun () -> walkType 0 f.FieldType)

                safe
                    ()
                    (fun () ->
                        f.DeclaringEntity
                        |> Option.iter (walkEntity 0)
                    )
            | :? FSharpUnionCase as uc -> safe () (fun () -> walkEntity 0 uc.DeclaringEntity)
            | _ -> ()

        let differentRefs =
            noNameByDisplay
            |> Seq.choose (fun kv ->
                let distinctRefs =
                    kv.Value
                    |> List.distinctBy (fun e -> RuntimeHelpers.GetHashCode(e))
                    |> List.length

                if distinctRefs > 1 then
                    Some(kv.Key, distinctRefs)
                else
                    None
            )
            |> Seq.toList

        {
            CallCount = callCount
            MaxDepth = maxDepth
            EntitiesByName = nameCache.Count
            EntitiesByRef = refCache.Count
            NoNameEntityEncounters = noNameHits
            DifferentRefNoNameEntities = differentRefs
            HitLimit =
                callCount
                >= limit
        }

    [<Test>]
    let ``entityToV1 traversal is bounded for record type`` () =
        let source =
            """
module M
type Foo = { X: int; Y: string; Z: float }
let f (x: Foo) = x.X
"""

        let ctx = getContext projectOptions source
        let symbols = ctx.CheckFileResults.GetAllUsesOfAllSymbolsInFile()
        let stats = walkFrom symbols

        printfn "=== Record type ==="
        printfn "Calls: %d  MaxDepth: %d" stats.CallCount stats.MaxDepth
        printfn "Entities by name: %d  by ref: %d" stats.EntitiesByName stats.EntitiesByRef
        printfn "No-name encounters: %d" stats.NoNameEntityEncounters

        for name, refs in stats.DifferentRefNoNameEntities do
            printfn "!! DIFFERENT REFS for no-name entity '%s': %d distinct objects" name refs

        Assert.That(stats.HitLimit, Is.False, "walk should terminate")

        Assert.That(
            stats.DifferentRefNoNameEntities,
            Is.Empty,
            "no no-name entity should have multiple distinct FCS object references"
        )

    [<Test>]
    let ``entityToV1 traversal is bounded for code with System.Type`` () =
        let source =
            """
module M
open System
open System.Collections.Generic

type MyRecord = { Name: string; Value: int; Created: DateTime }
let f (r: MyRecord) = r.Name
let g () : Dictionary<string, int> = Dictionary()
let h (t: Type) = t.Name
"""

        let ctx = getContext projectOptions source
        let symbols = ctx.CheckFileResults.GetAllUsesOfAllSymbolsInFile()
        let stats = walkFrom symbols

        printfn "=== Complex BCL types ==="
        printfn "Calls: %d  MaxDepth: %d" stats.CallCount stats.MaxDepth
        printfn "Entities by name: %d  by ref: %d" stats.EntitiesByName stats.EntitiesByRef
        printfn "No-name encounters: %d" stats.NoNameEntityEncounters

        for name, refs in stats.DifferentRefNoNameEntities do
            printfn "!! DIFFERENT REFS for no-name entity '%s': %d distinct objects" name refs

        Assert.That(stats.HitLimit, Is.False, "walk should terminate")

        Assert.That(
            stats.DifferentRefNoNameEntities,
            Is.Empty,
            "no no-name entity should have multiple distinct FCS object references"
        )

    [<Test>]
    let ``contextToV1 completes without overflow for type abbreviations`` () =
        let source =
            """
module M

let a (x: int) (y: bool) (z: string) (w: float) (u: unit) (o: obj) = ()
let b : int list = []
let c : bool option = None
let d = System.Int32.MaxValue
"""

        let ctx = getContext projectOptions source
        // This would stack-overflow before the AccessPath.DisplayName fix.
        let v1Ctx = contextToV1 ctx
        Assert.IsNotNull(v1Ctx, "contextToV1 should complete without overflow")
