namespace FSharp.Analyzers.SDK

open System
open System.IO
open System.Collections.Concurrent
open System.Reflection
open System.Runtime.Loader
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
                elif
                    m.ReturnType.FullName.StartsWith
                        "Microsoft.FSharp.Collections.FSharpList`1[[FSharp.Analyzers.SDK.Message"
                then
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
                Directory.GetFiles(dir, "*Analyzer*.dll", SearchOption.AllDirectories)
                |> Array.filter (fun a -> not (a.EndsWith("FSharp.Analyzers.SDK.dll")))
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

            let currentFSharpAnalyzersSDKVersion =
                Assembly.GetExecutingAssembly().GetName().Version

            let findFSharpAnalyzerSDKVersion (assembly: Assembly) =
                let references = assembly.GetReferencedAssemblies()
                let fas = references |> Array.find (fun ra -> ra.Name = "FSharp.Analyzers.SDK")
                fas.Version

            let analyzers =
                analyzerAssemblies
                |> Array.filter (fun (name, analyzerAssembly) ->
                    let version = findFSharpAnalyzerSDKVersion analyzerAssembly

                    if version = currentFSharpAnalyzersSDKVersion then
                        true
                    else
                        printError
                            $"Trying to load %s{name} which was built using SDK version %A{version}. Expect %A{currentFSharpAnalyzersSDKVersion} instead. Assembly will be skipped."

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

                if List.isEmpty analyzers then
                    failwith "no analyzers"

                registeredAnalyzers.AddOrUpdate(path, analyzers, (fun _ _ -> analyzers))
                |> ignore

            if registeredAnalyzers.Count = 0 then
                failwith "Nothing was added"

            Seq.length analyzers, analyzers |> Seq.collect snd |> Seq.length
        else
            0, 0

    member x.RunAnalyzers(ctx: 'TContext) : Message array =
        let analyzers = registeredAnalyzers.Values |> Seq.collect id

        analyzers
        |> Seq.collect (fun (_analyzerName, analyzer) ->
            try
                analyzer ctx
            with error ->
                []
        )
        |> Seq.toArray

    member x.RunAnalyzersSafely(ctx: 'TContext) : AnalysisResult list =
        let analyzers = registeredAnalyzers.Values |> Seq.collect id

        analyzers
        |> Seq.map (fun (analyzerName, analyzer) ->
            {
                AnalyzerName = analyzerName
                Output =
                    try
                        Ok(analyzer ctx)
                    with error ->
                        Result.Error error
            }
        )
        |> Seq.toList
