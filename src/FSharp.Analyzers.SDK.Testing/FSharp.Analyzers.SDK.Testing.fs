module FSharp.Analyzers.SDK.Testing

// Don't warn about using NotifyFileChanged of the FCS API
#nowarn "57"

open FSharp.Compiler.Text
open Microsoft.Build.Logging.StructuredLogger
open CliWrap
open System
open System.IO
open System.Collections.Generic
open System.Collections.ObjectModel
open FSharp.Compiler.CodeAnalysis

type FSharpProjectOptions with

    static member zero =
        {
            ProjectFileName = ""
            ProjectId = None
            SourceFiles = [||]
            OtherOptions = [||]
            ReferencedProjects = [||]
            IsIncompleteTypeCheckEnvironment = false
            UseScriptResolutionRules = false
            LoadTime = DateTime.UtcNow
            UnresolvedReferences = None
            OriginalLoadReferences = []
            Stamp = None
        }

type Package =
    {
        Name: string
        Version: string
    }

    override x.ToString() = $"{x.Name}_{x.Version}"

let fsharpFiles = set [| ".fs"; ".fsi"; ".fsx" |]

let isFSharpFile (file: string) =
    Seq.exists (fun (ext: string) -> file.EndsWith ext) fsharpFiles

let readCompilerArgsFromBinLog (build: Build) =
    if not build.Succeeded then
        failwith $"Build failed: {build.LogFilePath}"

    let projectName =
        build.Children
        |> Seq.choose (
            function
            | :? Project as p -> Some p.Name
            | _ -> None
        )
        |> Seq.distinct
        |> Seq.exactlyOne

    let message (fscTask: FscTask) =
        fscTask.Children
        |> Seq.tryPick (
            function
            | :? Message as m when m.Text.Contains "fsc" -> Some m.Text
            | _ -> None
        )

    let mutable args = None

    build.VisitAllChildren<Task>(fun task ->
        match task with
        | :? FscTask as fscTask ->
            match fscTask.Parent.Parent with
            | :? Project as p when p.Name = projectName -> args <- message fscTask
            | _ -> ()
        | _ -> ()
    )

    match args with
    | None -> failwith $"Could not parse binlog at {build.LogFilePath}, does it contain CoreCompile?"
    | Some args ->
        let idx = args.IndexOf "-o:"
        args.Substring(idx).Split [| '\n' |]

let mkOptions (compilerArgs: string array) =
    let sourceFiles =
        compilerArgs
        |> Array.filter (fun (line: string) -> isFSharpFile line && File.Exists line)

    let otherOptions =
        compilerArgs |> Array.filter (fun line -> not (isFSharpFile line))

    {
        ProjectFileName = "Project"
        ProjectId = None
        SourceFiles = sourceFiles
        OtherOptions = otherOptions
        ReferencedProjects = [||]
        IsIncompleteTypeCheckEnvironment = false
        UseScriptResolutionRules = false
        LoadTime = DateTime.Now
        UnresolvedReferences = None
        OriginalLoadReferences = []
        Stamp = None
    }

let mkOptionsFromBinaryLog build =
    let compilerArgs = readCompilerArgsFromBinLog build
    mkOptions compilerArgs

let getCachedIfOldBuildSucceeded binLogPath =
    if File.Exists binLogPath then
        let build = BinaryLog.ReadBuild binLogPath

        if build.Succeeded then
            Some build
        else
            File.Delete binLogPath
            None
    else
        None

let createProject (binLogPath: string) (tmpProjectDir: string) (framework: string) (additionalPkgs: Package list) =
    let stdOutBuffer = System.Text.StringBuilder()
    let stdErrBuffer = System.Text.StringBuilder()

    task {
        try
            Directory.CreateDirectory(tmpProjectDir) |> ignore

            // needed to escape the global.json circle of influence in a unit testing process
            let envDic = Dictionary<string, string>()
            envDic["MSBuildExtensionsPath"] <- null
            envDic["MSBuildSDKsPath"] <- null
            let roDic = ReadOnlyDictionary(envDic)

            let! _ =
                Cli
                    .Wrap("dotnet")
                    .WithWorkingDirectory(tmpProjectDir)
                    .WithArguments($"new classlib -f {framework} -lang F#")
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                    .WithValidation(CommandResultValidation.ZeroExitCode)
                    .ExecuteAsync()

            for p in additionalPkgs do
                let! _ =
                    Cli
                        .Wrap("dotnet")
                        .WithEnvironmentVariables(roDic)
                        .WithWorkingDirectory(tmpProjectDir)
                        .WithArguments($"add package {p.Name} --version {p.Version}")
                        .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                        .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                        .WithValidation(CommandResultValidation.ZeroExitCode)
                        .ExecuteAsync()

                ()

            let! _ =
                Cli
                    .Wrap("dotnet")
                    .WithEnvironmentVariables(roDic)
                    .WithWorkingDirectory(tmpProjectDir)
                    .WithArguments($"build -bl:{binLogPath}")
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                    .WithValidation(CommandResultValidation.ZeroExitCode)
                    .ExecuteAsync()

            return ()
        with e ->
            printfn $"StdOut:\n%s{stdOutBuffer.ToString()}"
            printfn $"StdErr:\n%s{stdErrBuffer.ToString()}"
            printfn $"Exception:\n%s{e.ToString()}"
    }

let mkOptionsFromProject (framework: string) (additionalPkgs: Package list) =
    task {
        try
            let id = Guid.NewGuid().ToString("N")
            let tmpProjectDir = Path.Combine(Path.GetTempPath(), id)

            let uniqueBinLogName =
                let packages =
                    additionalPkgs |> List.map (fun p -> p.ToString()) |> String.concat "_"

                $"v{Utils.currentFSharpAnalyzersSDKVersion}_{framework}_{packages}.binlog"

            let binLogCache =
                Path.Combine(Path.GetTempPath(), "FSharp.Analyzers.SDK.BinLogCache")

            let binLogPath = Path.Combine(binLogCache, uniqueBinLogName)

            let! binLogFile =
                let cached = getCachedIfOldBuildSucceeded binLogPath

                match cached with
                | Some f -> task { return f }
                | None ->
                    task {
                        Directory.CreateDirectory(binLogCache) |> ignore
                        let! _ = createProject binLogPath tmpProjectDir framework additionalPkgs
                        return BinaryLog.ReadBuild binLogPath
                    }

            return mkOptionsFromBinaryLog binLogFile
        with e ->
            printfn $"Exception:\n%s{e.ToString()}"
            return FSharpProjectOptions.zero
    }

let getContext (opts: FSharpProjectOptions) source =
    let fileName = "A.fs"
    let files = Map.ofArray [| (fileName, SourceText.ofString source) |]

    let documentSource fileName =
        Map.tryFind fileName files |> async.Return

    let fcs = Utils.createFCS (Some documentSource)
    let printError (s: string) = Console.WriteLine(s)
    let pathToAnalyzerDlls = Path.GetFullPath(".")

    let foundDlls, registeredAnalyzers =
        let client = Client<CliAnalyzerAttribute, CliContext>()
        client.LoadAnalyzers printError pathToAnalyzerDlls

    if foundDlls = 0 then
        failwith $"no Dlls found in {pathToAnalyzerDlls}"

    if registeredAnalyzers = 0 then
        failwith $"no Analyzers found in {pathToAnalyzerDlls}"

    let opts =
        { opts with
            SourceFiles = [| fileName |]
        }

    fcs.NotifyFileChanged(fileName, opts) |> Async.RunSynchronously // workaround for https://github.com/dotnet/fsharp/issues/15960
    let checkProjectResults = fcs.ParseAndCheckProject(opts) |> Async.RunSynchronously
    let allSymbolUses = checkProjectResults.GetAllUsesOfAllSymbols()

    if Array.isEmpty allSymbolUses then
        failwith "no symboluses"

    let printError s = printf $"{s}"

    match Utils.typeCheckFile fcs printError opts fileName (Utils.SourceOfSource.DiscreteSource source) with
    | Some(parseFileResults, checkFileResults) ->
        let sourceText = SourceText.ofString source
        Utils.createContext checkProjectResults fileName sourceText (parseFileResults, checkFileResults)
    | None -> failwith "typechecking file failed"

module Assert =

    let hasWarningsInLines (expectedLines: Set<int>) (msgs: FSharp.Analyzers.SDK.Message list) =
        let msgLines = msgs |> List.map (fun m -> m.Range.StartLine) |> Set.ofList
        msgLines = expectedLines

    let messageContains (expectedContent: string) (msg: FSharp.Analyzers.SDK.Message) =
        not (String.IsNullOrWhiteSpace(msg.Message))
        && msg.Message.Contains(expectedContent)

    let allMessagesContain (expectedContent: string) (msgs: FSharp.Analyzers.SDK.Message list) =
        msgs |> List.forall (messageContains expectedContent)

    let messageContainsAny (expectedContents: string list) (msg: FSharp.Analyzers.SDK.Message) =
        expectedContents |> List.exists msg.Message.Contains

    let messagesContainAny (expectedContents: string list) (msgs: FSharp.Analyzers.SDK.Message list) =
        msgs |> List.forall (messageContainsAny expectedContents)
