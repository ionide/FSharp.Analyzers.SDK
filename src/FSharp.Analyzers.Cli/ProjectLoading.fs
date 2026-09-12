module FSharp.Analyzers.Cli.ProjectLoading

open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open GlobExpressions
open Microsoft.Extensions.Logging
open Ionide.ProjInfo

/// <summary>Runs MSBuild to create FSharpProjectOptions based on the projPaths.</summary>
/// <returns>Returns only the FSharpProjectOptions based on the projPaths and not any referenced projects.</returns>
let loadProjects
    (logger: ILogger)
    toolsPath
    properties
    (projPaths: string list)
    (binLogPath: DirectoryInfo option)
    =
    async {
        let projPaths =
            projPaths
            |> List.map (fun proj ->
                Path.Combine(Environment.CurrentDirectory, proj)
                |> Path.GetFullPath
            )

        for proj in projPaths do
            logger.LogInformation("Loading project {0}", proj)

        let loader = WorkspaceLoader.Create(toolsPath, properties)

        binLogPath
        |> Option.iter (fun path ->
            logger.LogInformation("Using binary log path: {0}", path.FullName)
        )

        let binLogConfig =
            binLogPath
            |> Option.map (fun path -> BinaryLogGeneration.Within path)
            |> Option.defaultValue BinaryLogGeneration.Off

        let projectOptions = loader.LoadProjects(projPaths, [], binaryLog = binLogConfig)

        let failedLoads =
            projPaths
            |> Seq.filter (fun path ->
                not (
                    projectOptions
                    |> Seq.exists (fun p -> p.ProjectFileName = path)
                )
            )
            |> Seq.toList

        if Seq.length failedLoads > 0 then
            logger.LogError("Failed to load project '{0}'", failedLoads)
            exit (int ExitErrorCodes.FailedToLoadProject)

        let loaded =
            FCS.mapManyOptions projectOptions
            |> Seq.filter (fun p ->
                projPaths
                |> List.exists (fun x -> x = p.ProjectFileName)
            ) // We only want to analyze what was passed in
            |> Seq.toList

        return loaded
    }

let fsharpFiles =
    set
        [|
            ".fs"
            ".fsi"
            ".fsx"
        |]

let isFSharpFile (file: string) =
    Set.exists (fun (ext: string) -> file.EndsWith(ext, StringComparison.Ordinal)) fsharpFiles

/// <summary>Reads FSC compiler arguments from a response (RSP) file.</summary>
/// <remarks>
/// RSP files contain compiler arguments, with each argument on a separate line.
/// Lines starting with '#' are treated as comments and ignored.
/// Empty lines are ignored.
/// </remarks>
let readFscArgsFromFile (logger: ILogger) (filePath: string) : string =
    if not (File.Exists filePath) then
        logger.LogError("FSC args file not found: {0}", filePath)
        exit (int ExitErrorCodes.EmptyFscArgs)

    logger.LogInformation("Reading FSC arguments from file: {0}", filePath)

    let args =
        File.ReadAllLines(filePath)
        |> Array.choose (fun line ->
            let trimmed = line.Trim()

            if
                String.IsNullOrWhiteSpace trimmed
                || trimmed.StartsWith("#")
            then
                None
            else
                Some trimmed
        )
        |> String.concat ";"

    if String.IsNullOrWhiteSpace args then
        logger.LogError("No valid FSC arguments found in file: {0}", filePath)
        exit (int ExitErrorCodes.EmptyFscArgs)

    args

/// Resolves `--script` arguments, which may be globs, to absolute file paths.
let resolveScriptPaths (cwd: DirectoryInfo) (scriptGlobs: string list) =
    let beginsWithCurrentPath (path: string) =
        path.StartsWith("./")
        || path.StartsWith(".\\")

    scriptGlobs
    |> List.collect (fun scriptGlob ->
        let root, scriptGlob =
            if Path.IsPathRooted scriptGlob then
                // Glob can't handle absolute paths, so we need to make sure the scriptGlob is a relative path
                let root = Path.GetPathRoot scriptGlob
                let glob = scriptGlob.Substring(root.Length)
                DirectoryInfo root, glob
            else if beginsWithCurrentPath scriptGlob then
                // Glob can't handle relative paths starting with "./" or ".\", so we need trim it
                let relativeGlob = scriptGlob.Substring(2) // remove "./" or ".\"
                cwd, relativeGlob
            else
                cwd, scriptGlob

        root.GlobFiles scriptGlob
        |> Seq.map (fun file -> file.FullName)
        |> Seq.toList
    )

/// Creates FSharpProjectOptions for each script.
let loadScripts (logger: ILogger) (checker: FSharpChecker) (scripts: string list) =
    scripts
    |> List.map (fun script ->
        async {
            let! fileContent =
                File.ReadAllTextAsync script
                |> Async.AwaitTask

            let sourceText = SourceText.ofString fileContent
            // GetProjectOptionsFromScript cannot be run in parallel, it is not thread-safe.
            let! options, diagnostics =
                checker.GetProjectOptionsFromScript(
                    script,
                    sourceText,
                    // Without these, the script is resolved against the .NET Framework reference assemblies.
                    // FSharp.Core then fails to load and every construct that comes from it (printfn, string, int, ...)
                    // becomes an error-recovery node in the typed tree, which analyzers silently skip.
                    // See https://github.com/ionide/FSharp.Analyzers.SDK/issues/332
                    assumeDotNetFramework = false,
                    useSdkRefs = true
                )

            if not (List.isEmpty diagnostics) then
                diagnostics
                |> List.iter (fun d ->
                    logger.LogError(
                        "Script {0} has a diagnostic: {1} at {2}",
                        script,
                        d.Message,
                        d.Range
                    )
                )

            return options
        }
    )
    |> Async.Sequential
