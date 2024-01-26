namespace FSharp.Analyzers.SDK

open System
open System.IO
open System.Collections.Concurrent
open System.Reflection
open System.Runtime.Loader
open System.Text.RegularExpressions
open McMaster.NETCore.Plugins
open Microsoft.Extensions.Logging

type AnalysisResult =
    {
        AnalyzerName: string
        Output: Result<Message list, exn>
    }

module Client =

    type RegisteredAnalyzer<'TContext when 'TContext :> Context> =
        {
            AssemblyPath: string
            Name: string
            Analyzer: Analyzer<'TContext>
            ShortDescription: string option
            HelpUri: string option
        }

    let isAnalyzer<'TAttribute when 'TAttribute :> AnalyzerAttribute> (mi: MemberInfo) =
        mi.GetCustomAttributes true
        |> Array.tryFind (fun n -> n.GetType().Name = typeof<'TAttribute>.Name)
        |> Option.map unbox<'TAttribute>

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
            elif t.Name = "FSharpAsync`1" && t.GenericTypeArguments.Length = 1 then
                let listType = t.GenericTypeArguments.[0]

                if listType.Name = "FSharpList`1" && listType.GenericTypeArguments.Length = 1 then
                    // This could still be generic, as in an empty list is returned from the analyzer
                    let msgType = listType.GenericTypeArguments.[0]
                    msgType.Name = "a" || msgType = typeof<Message>
                else
                    false
            else
                false

        let getAnalyzerFromMemberInfo mi =
            match box mi with
            | :? FieldInfo as m ->
                if m.FieldType = typeof<Analyzer<'TContext>> then
                    Some(m.GetValue(null) |> unboxAnalyzer)
                else
                    None
            | :? MethodInfo as m ->
                if m.ReturnType = typeof<Analyzer<'TContext>> then
                    Some(m.Invoke(null, null) |> unboxAnalyzer)
                elif hasExpectReturnType m.ReturnType then
                    try
                        let analyzer: Analyzer<'TContext> = fun ctx -> m.Invoke(null, [| ctx |]) |> unbox
                        Some analyzer
                    with ex ->
                        None
                else
                    None
            | :? PropertyInfo as m ->
                if m.PropertyType = typeof<Analyzer<'TContext>> then
                    Some(m.GetValue(null, null) |> unboxAnalyzer)
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

    let analyzersFromType<'TAnalyzerAttribute, 'TContext
        when 'TAnalyzerAttribute :> AnalyzerAttribute and 'TContext :> Context>
        (path: string)
        (t: Type)
        : RegisteredAnalyzer<'TContext> list
        =
        let asMembers x = Seq.map (fun m -> m :> MemberInfo) x
        let bindingFlags = BindingFlags.Public ||| BindingFlags.Static

        let members =
            [
                t.GetTypeInfo().GetMethods bindingFlags |> asMembers
                t.GetTypeInfo().GetProperties bindingFlags |> asMembers
                t.GetTypeInfo().GetFields bindingFlags |> asMembers
            ]
            |> Seq.collect id

        members
        |> Seq.choose (analyzerFromMember<'TAnalyzerAttribute, 'TContext> path)
        |> Seq.toList

type AssemblyLoadStats =
    {
        AnalyzerAssemblies: int
        Analyzers: int
        FailedAssemblies: int
    }

type ExcludeInclude =
    | ExcludeFilter of (string -> bool)
    | IncludeFilter of (string -> bool)

type Client<'TAttribute, 'TContext when 'TAttribute :> AnalyzerAttribute and 'TContext :> Context>(logger: ILogger) =
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
                            )

                        Some(analyzerDll, analyzerLoader.LoadDefaultAssembly())
                    with _ ->
                        None
                )

            let findFSharpAnalyzerSDKVersion (assembly: Assembly) =
                let references = assembly.GetReferencedAssemblies()
                let fas = references |> Array.find (fun ra -> ra.Name = "FSharp.Analyzers.SDK")
                fas.Version

            let skippedAssemblies = ref 0

            let analyzers =
                analyzerAssemblies
                |> Array.filter (fun (name, analyzerAssembly) ->
                    let version = findFSharpAnalyzerSDKVersion analyzerAssembly

                    if
                        version.Major = Utils.currentFSharpAnalyzersSDKVersion.Major
                        && version.Minor = Utils.currentFSharpAnalyzersSDKVersion.Minor
                    then
                        true
                    else
                        System.Threading.Interlocked.Increment skippedAssemblies |> ignore

                        logger.LogError(
                            "Trying to load {Name} which was built using SDK version {Version}. Expect {SdkVersion} instead. Assembly will be skipped.",
                            name,
                            version,
                            Utils.currentFSharpAnalyzersSDKVersion
                        )

                        false
                )
                |> Array.map (fun (path, assembly) ->
                    let analyzers =
                        assembly.GetExportedTypes()
                        |> Seq.collect (Client.analyzersFromType<'TAttribute, 'TContext> path)
                        |> Seq.filter (fun registeredAnalyzer ->
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
                        )
                        |> Seq.toList

                    path, analyzers
                )

            for path, analyzers in analyzers do
                registeredAnalyzers.AddOrUpdate(path, analyzers, (fun _ _ -> analyzers))
                |> ignore

            let assemblyCount = Array.length analyzers
            let analyzerCount = analyzers |> Seq.sumBy (snd >> Seq.length)

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
        async {
            let analyzers = registeredAnalyzers.Values |> Seq.collect id

            let! messagesPerAnalyzer =
                analyzers
                |> Seq.map (fun registeredAnalyzer ->
                    try
                        async {
                            let! messages = registeredAnalyzer.Analyzer ctx

                            return
                                messages
                                |> List.map (fun message ->
                                    {
                                        Message = message
                                        Name = registeredAnalyzer.Name
                                        AssemblyPath = registeredAnalyzer.AssemblyPath
                                        ShortDescription = registeredAnalyzer.ShortDescription
                                        HelpUri = registeredAnalyzer.HelpUri
                                    }
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
        async {
            let analyzers = registeredAnalyzers.Values |> Seq.collect id

            let! results =
                analyzers
                |> Seq.map (fun registeredAnalyzer ->
                    async {
                        try
                            let! result = registeredAnalyzer.Analyzer ctx

                            return
                                {
                                    AnalyzerName = registeredAnalyzer.Name
                                    Output = Result.Ok result
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
