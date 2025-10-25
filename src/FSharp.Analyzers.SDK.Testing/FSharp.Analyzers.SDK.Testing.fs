module FSharp.Analyzers.SDK.Testing

// Don't warn about using NotifyFileChanged of the FCS API
#nowarn "57"

open Microsoft.Build.Logging.StructuredLogger
open Microsoft.Extensions.Logging
open CliWrap
open System
open System.IO
open System.Collections.Generic
open System.Collections.ObjectModel
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Text
open FSharp.Compiler.CodeAnalysis.ProjectSnapshot

type FSharpChecker with

    member x.ParseAndCheckProject(opts: AnalyzerProjectOptions) : Async<FSharpCheckProjectResults> =
        match opts with
        | BackgroundCompilerOptions options -> x.ParseAndCheckProject options
        | TransparentCompilerOptions snapshot -> x.ParseAndCheckProject snapshot

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

type FSharpProjectSnapshot with

    static member zero =
        FSharpProjectSnapshot.Create(
            "",
            None,
            None,
            [],
            [],
            [],
            [],
            false,
            false,
            DateTime.UtcNow,
            None,
            [],
            None
        )

type Package =
    {
        Name: string
        Version: string
    }

    override x.ToString() = $"{x.Name}_{x.Version}"

exception CompilerDiagnosticErrors of FSharpDiagnostic array

let fsharpFiles =
    set
        [|
            ".fs"
            ".fsi"
            ".fsx"
        |]

let isFSharpFile (file: string) =
    Set.exists (fun (ext: string) -> file.EndsWith(ext, StringComparison.Ordinal)) fsharpFiles

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
    | None ->
        failwith $"Could not parse binlog at {build.LogFilePath}, does it contain CoreCompile?"
    | Some args ->
        let idx = args.IndexOf("-o:", StringComparison.Ordinal)
        args.Substring(idx).Split [| '\n' |]

let mkOptions (compilerArgs: string array) =
    let sourceFiles =
        compilerArgs
        |> Array.filter (fun (line: string) ->
            isFSharpFile line
            && File.Exists line
        )

    let otherOptions =
        compilerArgs
        |> Array.filter (fun line -> not (isFSharpFile line))

    {
        ProjectFileName = "Project"
        ProjectId = None
        SourceFiles = sourceFiles
        OtherOptions = otherOptions
        ReferencedProjects = [||]
        IsIncompleteTypeCheckEnvironment = false
        UseScriptResolutionRules = false
        LoadTime = DateTime.UtcNow
        UnresolvedReferences = None
        OriginalLoadReferences = []
        Stamp = None
    }

let mkSnapshot (compilerArgs: string array) =

    let sourceFiles =
        compilerArgs
        |> Array.choose (fun (line: string) ->
            if
                isFSharpFile line
                && File.Exists line
            then

                FSharpFileSnapshot.CreateFromFileSystem(line)
                |> Some
            else
                None
        )
        |> Array.toList

    let otherOptions =
        compilerArgs
        |> Array.filter (fun line -> not (isFSharpFile line))
        |> Array.toList

    FSharpProjectSnapshot.Create(
        "Project",
        None,
        None,
        sourceFiles,
        [],
        otherOptions,
        [],
        false,
        false,
        DateTime.UtcNow,
        None,
        [],
        None

    )

let mkOptionsFromBinaryLog build =
    let compilerArgs = readCompilerArgsFromBinLog build
    mkOptions compilerArgs

let mkSnapshotFromBinaryLog build =
    let compilerArgs = readCompilerArgsFromBinLog build
    mkSnapshot compilerArgs

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

let createProject
    (binLogPath: string)
    (tmpProjectDir: string)
    (framework: string)
    (additionalPkgs: Package list)
    =
    let stdOutBuffer = System.Text.StringBuilder()
    let stdErrBuffer = System.Text.StringBuilder()

    task {
        try
            Directory.CreateDirectory(tmpProjectDir)
            |> ignore

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
                    .WithArguments($"build -bl:\"{binLogPath}\"")
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

open System.Threading.Tasks

let mkBinlog (framework: string) (additionalPkgs: Package list) =
    task {
        try
            let id = Guid.NewGuid().ToString("N")
            let tmpProjectDir = Path.Combine(Path.GetTempPath(), id)

            let uniqueBinLogName =
                let packages =
                    additionalPkgs
                    |> List.map (fun p -> p.ToString())
                    |> String.concat "_"

                $"v{Utils.currentFSharpAnalyzersSDKVersion}_{framework}_{packages}.binlog"

            let binLogCache =
                Path.Combine(Path.GetTempPath(), "FSharp.Analyzers.SDK.BinLogCache")

            let binLogPath = Path.Combine(binLogCache, uniqueBinLogName)

            let! binLogFile =
                let cached = getCachedIfOldBuildSucceeded binLogPath

                match cached with
                | Some f -> Task.FromResult f
                | None ->
                    task {
                        Directory.CreateDirectory(binLogCache)
                        |> ignore

                        let! _ = createProject binLogPath tmpProjectDir framework additionalPkgs
                        return BinaryLog.ReadBuild binLogPath
                    }

            return binLogFile
        with e ->
            printfn $"Exception:\n%s{e.ToString()}"
            return failwith "Could not create binlog"
    }

let mkOptionsFromProject (framework: string) (additionalPkgs: Package list) =
    task {
        try
            let! binLogFile = mkBinlog framework additionalPkgs
            return mkOptionsFromBinaryLog binLogFile
        with e ->
            printfn $"Exception:\n%s{e.ToString()}"
            return FSharpProjectOptions.zero
    }

let mkSnapshotFromProject (framework: string) (additionalPkgs: Package list) =
    task {
        try
            let! binLogFile = mkBinlog framework additionalPkgs
            return mkSnapshotFromBinaryLog binLogFile
        with e ->
            printfn $"Exception:\n%s{e.ToString()}"
            return FSharpProjectSnapshot.zero
    }

type SourceFile = { FileName: string; Source: string }

let getContextFor (opts: AnalyzerProjectOptions) allSources fileToAnalyze =
    task {

        let analyzedFileName = fileToAnalyze.FileName

        let docSourceMap =
            allSources
            |> List.map (fun sf -> sf.FileName, SourceText.ofString sf.Source)
            |> Map.ofList

        let documentSource fileName =
            Map.tryFind fileName docSourceMap
            |> async.Return

        let fcs = Utils.createFCS (Some documentSource)
        let pathToAnalyzerDlls = Path.GetFullPath(".")

        let assemblyLoadStats =
            let client = Client<CliAnalyzerAttribute, CliContext>()
            client.LoadAnalyzers pathToAnalyzerDlls

        if assemblyLoadStats.AnalyzerAssemblies = 0 then
            failwith $"no Dlls found in {pathToAnalyzerDlls}"

        if assemblyLoadStats.Analyzers = 0 then
            failwith $"no Analyzers found in {pathToAnalyzerDlls}"

        if assemblyLoadStats.FailedAssemblies > 0 then
            failwith
                $"failed to load %i{assemblyLoadStats.FailedAssemblies} Analyzers in {pathToAnalyzerDlls}"

        let! analyzerOpts =
            match opts with
            | BackgroundCompilerOptions bOpts ->
                task {

                    let allFileNames =
                        allSources
                        |> List.map (fun sf -> sf.FileName)
                        |> Array.ofList

                    let bOpts =
                        { bOpts with
                            SourceFiles = allFileNames
                        }

                    do! fcs.NotifyFileChanged(analyzedFileName, bOpts) // workaround for https://github.com/dotnet/fsharp/issues/15960
                    return BackgroundCompilerOptions bOpts
                }
            | TransparentCompilerOptions snap ->
                let docSource = DocumentSource.Custom documentSource

                let fileSnapshots =
                    allSources
                    |> List.map (fun sf ->
                        FSharpFileSnapshot.CreateFromDocumentSource(sf.FileName, docSource)
                    )

                let snap =
                    FSharpProjectSnapshot.Create(
                        snap.ProjectFileName,
                        snap.OutputFileName,
                        snap.ProjectId,
                        fileSnapshots,
                        snap.ReferencesOnDisk,
                        snap.OtherOptions,
                        snap.ReferencedProjects,
                        snap.IsIncompleteTypeCheckEnvironment,
                        snap.UseScriptResolutionRules,
                        snap.LoadTime,
                        snap.UnresolvedReferences,
                        snap.OriginalLoadReferences,
                        snap.Stamp
                    )

                snap
                |> TransparentCompilerOptions
                |> Task.FromResult

        let! checkProjectResults = fcs.ParseAndCheckProject analyzerOpts

        let allSymbolUses = checkProjectResults.GetAllUsesOfAllSymbols()

        if Array.isEmpty allSymbolUses then
            failwith "no symboluses"

        match!
            Utils.typeCheckFile
                fcs
                Abstractions.NullLogger.Instance
                analyzerOpts
                analyzedFileName
                (Utils.SourceOfSource.DiscreteSource fileToAnalyze.Source)
        with
        | Ok(parseFileResults, checkFileResults) ->
            let diagErrors =
                checkFileResults.Diagnostics
                |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)

            if not (Array.isEmpty diagErrors) then
                raise (CompilerDiagnosticErrors diagErrors)

            let sourceText = SourceText.ofString fileToAnalyze.Source

            return
                Utils.createContext
                    checkProjectResults
                    analyzedFileName
                    sourceText
                    (parseFileResults, checkFileResults)
                    analyzerOpts
        | Error e -> return failwith $"typechecking file failed: %O{e}"
    }

let getContext (opts: FSharpProjectOptions) source =
    let source = { FileName = "A.fs"; Source = source }

    (getContextFor (BackgroundCompilerOptions opts) [ source ] source).GetAwaiter().GetResult()

let getContextForSignature (opts: FSharpProjectOptions) source =
    let source = { FileName = "A.fsi"; Source = source }

    (getContextFor (BackgroundCompilerOptions opts) [ source ] source).GetAwaiter().GetResult()

module Assert =

    let hasWarningsInLines (expectedLines: Set<int>) (msgs: FSharp.Analyzers.SDK.Message list) =
        let msgLines =
            msgs
            |> List.map (fun m -> m.Range.StartLine)
            |> Set.ofList

        msgLines = expectedLines

    let messageContains (expectedContent: string) (msg: FSharp.Analyzers.SDK.Message) =
        not (String.IsNullOrWhiteSpace(msg.Message))
        && msg.Message.Contains(expectedContent)

    let allMessagesContain (expectedContent: string) (msgs: FSharp.Analyzers.SDK.Message list) =
        msgs
        |> List.forall (messageContains expectedContent)

    let messageContainsAny (expectedContents: string list) (msg: FSharp.Analyzers.SDK.Message) =
        expectedContents
        |> List.exists msg.Message.Contains

    let messagesContainAny
        (expectedContents: string list)
        (msgs: FSharp.Analyzers.SDK.Message list)
        =
        msgs
        |> List.forall (messageContainsAny expectedContents)
