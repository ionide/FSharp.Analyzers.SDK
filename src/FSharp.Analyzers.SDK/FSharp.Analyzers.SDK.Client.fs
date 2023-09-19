namespace FSharp.Analyzers.SDK

open System
open System.IO
open System.Collections.Concurrent
open System.Reflection
open System.Runtime.Loader
open System.Text.RegularExpressions
open McMaster.NETCore.Plugins

type AnalysisResult =
    {
        AnalyzerName: string
        Output: Result<Message list, exn>
    }

module Client =

    let isAnalyzer<'TAttribute when 'TAttribute :> AnalyzerAttribute> (mi: MemberInfo) =
        mi.GetCustomAttributes true
        |> Seq.tryFind (fun n -> n.GetType().Name = typeof<'TAttribute>.Name)
        |> Option.map unbox<'TAttribute>

    let analyzerFromMember<'TAnalyzerAttribute, 'TContext when 'TAnalyzerAttribute :> AnalyzerAttribute>
        (mi: MemberInfo)
        : (string * Analyzer<'TContext>) option
        =
        let inline unboxAnalyzer v =
            if isNull v then failwith "Analyzer is null" else unbox v

        let hasExpectReturnType (t: Type) =
            // t might be a System.RunTimeType as could have no FullName
            if not (isNull t.FullName) then
                t.FullName.StartsWith
                    "Microsoft.FSharp.Control.FSharpAsync`1[[Microsoft.FSharp.Collections.FSharpList`1[[FSharp.Analyzers.SDK.Message"
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
            | Some analyzer -> Some(analyzerAttribute.Name, analyzer)
            | None -> None
        | None -> None

    let analyzersFromType<'TAnalyzerAttribute, 'TContext when 'TAnalyzerAttribute :> AnalyzerAttribute>
        (t: Type)
        : (string * Analyzer<'TContext>) list
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
        |> Seq.choose analyzerFromMember<'TAnalyzerAttribute, 'TContext>
        |> Seq.toList

type Client<'TAttribute, 'TContext when 'TAttribute :> AnalyzerAttribute and 'TContext :> Context>() =
    let registeredAnalyzers =
        ConcurrentDictionary<string, (string * Analyzer<'TContext>) list>()

    member x.LoadAnalyzers (printError: string -> unit) (dir: string) : int * int =
        if Directory.Exists dir then
            let analyzerAssemblies =
                let regex = Regex(@".*test.*\.dll$")

                Directory.GetFiles(dir, "*Analyzer*.dll", SearchOption.AllDirectories)
                |> Array.filter (fun a ->
                    let s = Path.GetFileName(a).ToLowerInvariant()
                    not (s.EndsWith("fsharp.analyzers.sdk.dll") || regex.IsMatch(s))
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

            let analyzers =
                analyzerAssemblies
                |> Array.filter (fun (name, analyzerAssembly) ->
                    let version = findFSharpAnalyzerSDKVersion analyzerAssembly

                    if version = Utils.currentFSharpAnalyzersSDKVersion then
                        true
                    else
                        printError
                            $"Trying to load %s{name} which was built using SDK version %A{version}. Expect %A{Utils.currentFSharpAnalyzersSDKVersion} instead. Assembly will be skipped."

                        false
                )
                |> Array.map (fun (path, assembly) ->
                    let analyzers =
                        assembly.GetExportedTypes()
                        |> Seq.collect Client.analyzersFromType<'TAttribute, 'TContext>

                    path, analyzers
                )

            for path, analyzers in analyzers do
                let analyzers = Seq.toList analyzers

                registeredAnalyzers.AddOrUpdate(path, analyzers, (fun _ _ -> analyzers))
                |> ignore

            Seq.length analyzers, analyzers |> Seq.collect snd |> Seq.length
        else
            0, 0

    member x.RunAnalyzers(ctx: 'TContext) : Async<Message list> =
        async {
            let analyzers = registeredAnalyzers.Values |> Seq.collect id

            let! messagesPerAnalyzer =
                analyzers
                |> Seq.map (fun (_analyzerName, analyzer) ->
                    try
                        analyzer ctx
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
                |> Seq.map (fun (analyzerName, analyzer) ->
                    async {
                        try
                            let! result = analyzer ctx

                            return
                                {
                                    AnalyzerName = analyzerName
                                    Output = Result.Ok result
                                }
                        with error ->
                            return
                                {
                                    AnalyzerName = analyzerName
                                    Output = Result.Error error
                                }
                    }
                )
                |> Async.Parallel

            return List.ofArray results
        }
