namespace FSharp.Analyzers.SDK

open System
open System.IO
open System.Collections.Concurrent
open System.Reflection
open System.Runtime.Loader
open System.Text.RegularExpressions
open McMaster.NETCore.Plugins
open Microsoft.Extensions.Logging

type AnalyzerName = string

type AnalysisResult =
    {
        AnalyzerName: AnalyzerName
        Output: Result<Message list, exn>
    }

module Client =

    type RegisteredAnalyzer<'TContext when 'TContext :> Context> =
        {
            AssemblyPath: string
            Name: AnalyzerName
            Analyzer: Analyzer<'TContext>
            ShortDescription: string option
            HelpUri: string option
        }

    let isAnalyzer<'TAttribute when 'TAttribute :> AnalyzerAttribute>
        (mi: MemberInfo)
        : 'TAttribute option
        =
        mi.GetCustomAttributes true
        |> Array.tryFind (fun n -> n.GetType().Name = typeof<'TAttribute>.Name)
        |> Option.bind (fun attr ->
            try
                Some(unbox<'TAttribute> attr)
            with _ ->
                None
        )

    let analyzerFromMember<'TAnalyzerAttribute, 'TContext
        when 'TAnalyzerAttribute :> AnalyzerAttribute and 'TContext :> Context>
        (path: string)
        (mi: MemberInfo)
        : RegisteredAnalyzer<'TContext> option
        =
        let inline unboxAnalyzer v =
            if isNull v then failwith "Analyzer is null" else unbox v

        let hasExpectReturnType (t: Type) =
            // t might be a System.RunTimeType as could have no FullName
            if not (isNull t.FullName) then
                t.FullName.StartsWith(
                    "Microsoft.FSharp.Control.FSharpAsync`1[[Microsoft.FSharp.Collections.FSharpList`1[[FSharp.Analyzers.SDK.Message",
                    StringComparison.InvariantCulture
                )
            elif
                t.Name = "FSharpAsync`1"
                && t.GenericTypeArguments.Length = 1
            then
                let listType = t.GenericTypeArguments.[0]

                if
                    listType.Name = "FSharpList`1"
                    && listType.GenericTypeArguments.Length = 1
                then
                    // This could still be generic, as in an empty list is returned from the analyzer
                    let msgType = listType.GenericTypeArguments.[0]

                    msgType.Name = "a"
                    || msgType = typeof<Message>
                else
                    false
            else
                false

        let getAnalyzerFromMemberInfo mi =
            match box mi with
            | :? FieldInfo as m ->
                if m.FieldType = typeof<Analyzer<'TContext>> then
                    Some(
                        m.GetValue(null)
                        |> unboxAnalyzer
                    )
                else
                    None
            | :? MethodInfo as m ->
                if m.ReturnType = typeof<Analyzer<'TContext>> then
                    Some(
                        m.Invoke(null, null)
                        |> unboxAnalyzer
                    )
                elif hasExpectReturnType m.ReturnType then
                    try
                        let analyzer: Analyzer<'TContext> =
                            fun ctx ->
                                m.Invoke(null, [| ctx |])
                                |> unbox

                        Some analyzer
                    with ex ->
                        None
                else
                    None
            | :? PropertyInfo as m ->
                if m.PropertyType = typeof<Analyzer<'TContext>> then
                    Some(
                        m.GetValue(null, null)
                        |> unboxAnalyzer
                    )
                else
                    None
            | _ -> None

        match isAnalyzer<'TAnalyzerAttribute> mi with
        | Some analyzerAttribute ->
            match getAnalyzerFromMemberInfo mi with
            | Some analyzer ->
                let name =
                    if String.IsNullOrWhiteSpace analyzerAttribute.Name then
                        mi.Name
                    else
                        analyzerAttribute.Name

                Some
                    {
                        AssemblyPath = path
                        Name = name
                        Analyzer = analyzer
                        ShortDescription = analyzerAttribute.ShortDescription
                        HelpUri = analyzerAttribute.HelpUri
                    }

            | None -> None
        | None -> None

    let shouldIgnoreMessage (ctx: 'Context :> #Context) message =
        match
            ctx.AnalyzerIgnoreRanges
            |> Map.tryFind message.Code
        with
        | Some ignoreRanges ->
            ignoreRanges
            |> List.exists (
                function
                | File -> true
                | Range(commentStart, commentEnd) ->
                    if
                        message.Range.StartLine
                        - 1
                        >= commentStart
                        && message.Range.EndLine
                           - 1
                           <= commentEnd
                    then
                        true
                    else
                        false
                | NextLine line ->
                    if
                        message.Range.StartLine
                        - 1 = line
                    then
                        true
                    else
                        false
                | CurrentLine line -> if message.Range.StartLine = line then true else false
            )
        | None -> false

    let analyzersFromType<'TAnalyzerAttribute, 'TContext
        when 'TAnalyzerAttribute :> AnalyzerAttribute and 'TContext :> Context>
        (path: string)
        (t: Type)
        : RegisteredAnalyzer<'TContext> list
        =
        let asMembers x = Seq.map (fun m -> m :> MemberInfo) x

        let bindingFlags =
            BindingFlags.Public
            ||| BindingFlags.Static

        let members =
            [
                t.GetTypeInfo().GetMethods bindingFlags
                |> asMembers
                t.GetTypeInfo().GetProperties bindingFlags
                |> asMembers
                t.GetTypeInfo().GetFields bindingFlags
                |> asMembers
            ]
            |> Seq.collect id

        members
        |> Seq.choose (analyzerFromMember<'TAnalyzerAttribute, 'TContext> path)
        |> Seq.toList

module internal V1Support =

    let isV1CliAnalyzer (mi: MemberInfo) : FSharp.Analyzers.SDK.V1.CliAnalyzerAttribute option =
        mi.GetCustomAttributes true
        |> Array.tryFind (fun a ->
            a.GetType().FullName = "FSharp.Analyzers.SDK.V1.CliAnalyzerAttribute"
        )
        |> Option.bind (fun attr ->
            try
                Some(unbox<FSharp.Analyzers.SDK.V1.CliAnalyzerAttribute> attr)
            with _ ->
                None
        )

    let adaptV1Analyzer (v1Analyzer: FSharp.Analyzers.SDK.V1.Analyzer) : Analyzer<CliContext> =
        fun ctx ->
            async {
                let v1Ctx = AdapterV1.contextToV1 ctx
                let! v1Messages = v1Analyzer v1Ctx

                return
                    v1Messages
                    |> List.map AdapterV1.messageFromV1
            }

    let private v1ExpectedReturnType =
        typeof<Async<FSharp.Analyzers.SDK.V1.Message list>>

    let private hasV1ExpectReturnType (t: Type) = t = v1ExpectedReturnType

    let v1CliAnalyzerFromMember
        (path: string)
        (mi: MemberInfo)
        : Client.RegisteredAnalyzer<CliContext> option
        =
        let inline unboxV1Analyzer v =
            if isNull v then
                failwith "V1 Analyzer is null"
            else
                unbox<FSharp.Analyzers.SDK.V1.Analyzer> v

        let getV1Analyzer (mi: MemberInfo) : FSharp.Analyzers.SDK.V1.Analyzer option =
            try
                match box mi with
                | :? FieldInfo as m ->
                    if m.FieldType = typeof<FSharp.Analyzers.SDK.V1.Analyzer> then
                        Some(
                            m.GetValue(null)
                            |> unboxV1Analyzer
                        )
                    else
                        None
                | :? MethodInfo as m ->
                    if m.ReturnType = typeof<FSharp.Analyzers.SDK.V1.Analyzer> then
                        Some(
                            m.Invoke(null, null)
                            |> unboxV1Analyzer
                        )
                    elif hasV1ExpectReturnType m.ReturnType then
                        let analyzer: FSharp.Analyzers.SDK.V1.Analyzer =
                            fun ctx ->
                                m.Invoke(null, [| ctx |])
                                |> unbox

                        Some analyzer
                    else
                        None
                | :? PropertyInfo as m ->
                    if m.PropertyType = typeof<FSharp.Analyzers.SDK.V1.Analyzer> then
                        Some(
                            m.GetValue(null, null)
                            |> unboxV1Analyzer
                        )
                    else
                        None
                | _ -> None
            with _ ->
                None

        match isV1CliAnalyzer mi with
        | Some attr ->
            match getV1Analyzer mi with
            | Some v1Analyzer ->
                let name =
                    if String.IsNullOrWhiteSpace attr.Name then
                        mi.Name
                    else
                        attr.Name

                Some
                    {
                        AssemblyPath = path
                        Name = name
                        Analyzer = adaptV1Analyzer v1Analyzer
                        ShortDescription = attr.ShortDescription
                        HelpUri = attr.HelpUri
                    }
            | None -> None
        | None -> None

    let v1CliAnalyzersFromType
        (path: string)
        (t: Type)
        : Client.RegisteredAnalyzer<CliContext> list
        =
        let asMembers x = Seq.map (fun m -> m :> MemberInfo) x

        let bindingFlags =
            BindingFlags.Public
            ||| BindingFlags.Static

        let members =
            [
                t.GetTypeInfo().GetMethods bindingFlags
                |> asMembers
                t.GetTypeInfo().GetProperties bindingFlags
                |> asMembers
                t.GetTypeInfo().GetFields bindingFlags
                |> asMembers
            ]
            |> Seq.collect id

        members
        |> Seq.choose (v1CliAnalyzerFromMember path)
        |> Seq.toList

type AssemblyLoadStats =
    {
        AnalyzerAssemblies: int
        Analyzers: int
        FailedAssemblies: int
    }

type ExcludeInclude =
    | ExcludeFilter of (AnalyzerName -> bool)
    | IncludeFilter of (AnalyzerName -> bool)

type Client<'TAttribute, 'TContext when 'TAttribute :> AnalyzerAttribute and 'TContext :> Context>
    (logger: ILogger)
    =
    do TASTCollecting.logger <- logger

    let registeredAnalyzers =
        ConcurrentDictionary<string, Client.RegisteredAnalyzer<'TContext> list>()

    new() = Client(Abstractions.NullLogger.Instance)

    member x.LoadAnalyzers(dir: string, ?excludeInclude: ExcludeInclude) : AssemblyLoadStats =
        if Directory.Exists dir then
            let analyzerAssemblies =
                let regex = Regex(@".*test.*\.dll$")

                Directory.GetFiles(dir, "*Analyzer*.dll", SearchOption.AllDirectories)
                |> Array.filter (fun a ->
                    let s = Path.GetFileName(a)

                    not (
                        s.EndsWith("fsharp.analyzers.sdk.dll", StringComparison.OrdinalIgnoreCase)
                        || regex.IsMatch(s)
                    )
                )
                |> Array.choose (fun analyzerDll ->
                    try
                        // loads an assembly and all of it's dependencies
                        let analyzerLoader =
                            PluginLoader.CreateFromAssemblyFile(
                                analyzerDll,
                                fun config ->
                                    config.DefaultContext <- AssemblyLoadContext.Default
                                    config.PreferSharedTypes <- true
                                    config.LoadInMemory <- true
                            )

                        Some(analyzerDll, analyzerLoader.LoadDefaultAssembly())
                    with _ ->
                        None
                )

            let findFSharpAnalyzerSDKVersion (assembly: Assembly) =
                let references = assembly.GetReferencedAssemblies()

                let fas =
                    references
                    |> Array.find (fun ra -> ra.Name = "FSharp.Analyzers.SDK")

                fas.Version

            let skippedAssemblies = ref 0

            let filterByExcludeInclude
                (assembly: Assembly)
                (registeredAnalyzer: Client.RegisteredAnalyzer<'TContext>)
                =
                match excludeInclude with
                | Some(ExcludeFilter excludeFilter) ->
                    let shouldExclude = excludeFilter registeredAnalyzer.Name

                    if shouldExclude then
                        logger.LogInformation(
                            "Excluding {Name} from {FullName}",
                            registeredAnalyzer.Name,
                            assembly.FullName
                        )

                    not shouldExclude
                | Some(IncludeFilter includeFilter) ->
                    let shouldInclude = includeFilter registeredAnalyzer.Name

                    if shouldInclude then
                        logger.LogInformation(
                            "Including {Name} from {FullName}",
                            registeredAnalyzer.Name,
                            assembly.FullName
                        )

                    shouldInclude
                | None -> true

            let analyzers =
                analyzerAssemblies
                |> Array.map (fun (path, assembly) ->
                    let version = findFSharpAnalyzerSDKVersion assembly

                    let isVersionMatch =
                        version.Major = Utils.currentFSharpAnalyzersSDKVersion.Major
                        && version.Minor = Utils.currentFSharpAnalyzersSDKVersion.Minor

                    // V1 CLI analyzers: load regardless of version (no version gate).
                    let v1Analyzers: Client.RegisteredAnalyzer<'TContext> list =
                        if typeof<'TContext> = typeof<CliContext> then
                            try
                                assembly.GetExportedTypes()
                                |> Seq.collect (V1Support.v1CliAnalyzersFromType path)
                                |> Seq.map (fun ra ->
                                    unbox<Client.RegisteredAnalyzer<'TContext>> (box ra)
                                )
                                |> Seq.toList
                            with _ ->
                                []
                        else
                            []

                    // Legacy analyzers: version check required.
                    let legacyAnalyzers =
                        if isVersionMatch then
                            try
                                assembly.GetExportedTypes()
                                |> Seq.collect (
                                    Client.analyzersFromType<'TAttribute, 'TContext> path
                                )
                                |> Seq.toList
                            with _ ->
                                []
                        else
                            []

                    if
                        not isVersionMatch
                        && List.isEmpty v1Analyzers
                    then
                        System.Threading.Interlocked.Increment skippedAssemblies
                        |> ignore

                        logger.LogError(
                            "Trying to load {Name} which was built using SDK version {Version}. Expect {SdkVersion} instead. Assembly will be skipped.",
                            path,
                            version,
                            Utils.currentFSharpAnalyzersSDKVersion
                        )

                    let allAnalyzers =
                        (legacyAnalyzers
                         @ v1Analyzers)
                        |> List.filter (filterByExcludeInclude assembly)

                    path, allAnalyzers
                )

            for path, analyzers in analyzers do
                registeredAnalyzers.AddOrUpdate(path, analyzers, (fun _ _ -> analyzers))
                |> ignore

            let assemblyCount = Array.length analyzers

            let analyzerCount =
                analyzers
                |> Seq.sumBy (
                    snd
                    >> Seq.length
                )

            {
                AnalyzerAssemblies = assemblyCount
                Analyzers = analyzerCount
                FailedAssemblies = skippedAssemblies.Value
            }
        else
            logger.LogWarning("Analyzer path {analyzerPath} does not exist", dir)

            {
                AnalyzerAssemblies = 0
                Analyzers = 0
                FailedAssemblies = 0
            }

    member x.RunAnalyzers(ctx: 'TContext) : Async<AnalyzerMessage list> =
        x.RunAnalyzers(ctx, fun _ -> true)

    member x.RunAnalyzers
        (ctx: 'TContext, analyzerPredicate: Client.RegisteredAnalyzer<'TContext> -> bool)
        : Async<AnalyzerMessage list>
        =
        async {
            let analyzers =
                registeredAnalyzers.Values
                |> Seq.collect id
                |> Seq.filter analyzerPredicate

            let! messagesPerAnalyzer =
                analyzers
                |> Seq.map (fun registeredAnalyzer ->
                    try
                        async {
                            let! messages = registeredAnalyzer.Analyzer ctx

                            return
                                messages
                                |> List.choose (fun message ->
                                    let analyzerMessage =
                                        {
                                            Message = message
                                            Name = registeredAnalyzer.Name
                                            AssemblyPath = registeredAnalyzer.AssemblyPath
                                            ShortDescription = registeredAnalyzer.ShortDescription
                                            HelpUri = registeredAnalyzer.HelpUri
                                        }

                                    if Client.shouldIgnoreMessage ctx message then
                                        None
                                    else
                                        Some analyzerMessage
                                )
                        }
                    with error ->
                        async.Return []
                )
                |> Async.Parallel

            return
                [
                    for messages in messagesPerAnalyzer do
                        yield! messages
                ]
        }

    member x.RunAnalyzersSafely(ctx: 'TContext) : Async<AnalysisResult list> =
        x.RunAnalyzersSafely(ctx, fun _ -> true)

    member x.RunAnalyzersSafely
        (ctx: 'TContext, analyzerPredicate: Client.RegisteredAnalyzer<'TContext> -> bool)
        : Async<AnalysisResult list>
        =
        async {
            let analyzers =
                registeredAnalyzers.Values
                |> Seq.collect id
                |> Seq.filter analyzerPredicate

            let! results =
                analyzers
                |> Seq.map (fun registeredAnalyzer ->
                    async {
                        try
                            let! messages = registeredAnalyzer.Analyzer ctx

                            let messages =
                                messages
                                |> List.filter (
                                    Client.shouldIgnoreMessage ctx
                                    >> not
                                )

                            return
                                {
                                    AnalyzerName = registeredAnalyzer.Name
                                    Output = Result.Ok messages
                                }
                        with error ->
                            return
                                {
                                    AnalyzerName = registeredAnalyzer.Name
                                    Output = Result.Error error
                                }
                    }
                )
                |> Async.Parallel

            return List.ofArray results
        }
