#!/usr/bin/env -S dotnet fsi --

#r "nuget: Fun.Build, 1.1.18"
#r "nuget: Ionide.KeepAChangelog, 0.2.0"
#r "nuget: NuGet.Protocol, 7.9.0"

open Fun.Build
open System
open System.IO
open System.Threading
open Ionide.KeepAChangelog
open Ionide.KeepAChangelog.Domain
open NuGet.Common
open NuGet.Protocol
open NuGet.Protocol.Core.Types
open SemVersion

let purgeBinLogCache () =
    let binLogCache =
        Path.Combine(Path.GetTempPath(), "FSharp.Analyzers.SDK.BinLogCache")
    if Directory.Exists(binLogCache) then
        Directory.Delete(binLogCache, true)

let restoreStage =
    stage "restore" {
        run "dotnet tool restore"
        run "dotnet restore --locked-mode"
    }

let buildStage =
    stage "build" { run "dotnet build -c Release --no-restore -maxCpuCount" }

pipeline "Build" {
    restoreStage
    stage "lint" { run "dotnet fantomas check" }
    stage "build" { run "dotnet build -c Release --no-restore -maxCpuCount" }
    stage "test" {
        purgeBinLogCache ()
        run "dotnet test -c Release --no-build"
    }
    stage "sample" {
        run
            "dotnet run --project src/FSharp.Analyzers.Cli/FSharp.Analyzers.Cli.fsproj -- --project ./samples/OptionAnalyzer/OptionAnalyzer.fsproj --analyzers-path ./artifacts/bin/OptionAnalyzer/release --verbosity d --binlog-path temp/binlogs"
    }
    stage "docs" {
        run "dotnet fsdocs build --properties Configuration=Release --eval --clean --strict"
    }
    runIfOnlySpecified false
}

pipeline "ReleaseBuild" {
    restoreStage
    buildStage
    runIfOnlySpecified true
}

pipeline "Docs" {
    restoreStage
    buildStage
    stage "fsdocs" { run "dotnet fsdocs watch --properties Configuration=Release --eval" }
    runIfOnlySpecified true
}

let packageOutputDir =
    Path.Combine(__SOURCE_DIRECTORY__, "artifacts", "package", "release")

let packStage =
    stage "pack" {
        run "dotnet pack ./src/FSharp.Analyzers.SDK/FSharp.Analyzers.SDK.fsproj -c Release"
        run "dotnet pack ./src/FSharp.Analyzers.Cli/FSharp.Analyzers.Cli.fsproj -c Release"
        run
            "dotnet pack ./src/FSharp.Analyzers.SDK.Testing/FSharp.Analyzers.SDK.Testing.fsproj -c Release"
    }

let getLatestPublishedNugetVersion packageName =
    task {
        let logger = NullLogger.Instance
        let cancellationToken = CancellationToken.None

        let cache = new SourceCacheContext()
        let repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json")
        let! resource = repository.GetResourceAsync<FindPackageByIdResource>()
        let! versions =
            resource.GetAllVersionsAsync(packageName, cache, logger, cancellationToken)
        if Seq.isEmpty versions then
            return None
        else
            return versions |> Seq.max |> Some
    }

let getLatestChangeLogVersion () : SemanticVersion * DateTime * ChangelogData option =
    let changelog = FileInfo(Path.Combine(__SOURCE_DIRECTORY__, "CHANGELOG.md"))
    let changeLogResult =
        match Parser.parseChangeLog changelog with
        | Error error -> failwithf "%A" error
        | Ok result -> result

    changeLogResult.Releases
    |> List.sortByDescending (fun (_, d, _) -> d)
    |> List.head

let getPackageFiles (version: string) =
    if Directory.Exists packageOutputDir then
        Directory.GetFiles(packageOutputDir, "*.nupkg")
        |> Array.filter (fun file ->
            (Path.GetFileName file).EndsWith($".%s{version}.nupkg", StringComparison.Ordinal)
        )
    else
        [||]

type CommandRunner =
    abstract member LogWhenDryRun: string -> unit
    abstract member RunCommand: string -> Async<Result<unit, string>>
    abstract member RunCommandCaptureOutput: string -> Async<Result<string, string>>

/// Push the *.nupkg files for the given version.
let releaseNuGetPackages (ctx: CommandRunner) (version: string) =
    async {
        let key = Environment.GetEnvironmentVariable "NUGET_KEY"
        let packages = getPackageFiles version

        if Array.isEmpty packages then
            printfn "No packages found for version %s in %s" version packageOutputDir
            return 1
        else
            let mutable result = 0
            for package in packages do
                let! pushResult =
                    ctx.RunCommand
                        $"dotnet nuget push \"%s{package}\" --api-key %s{key} --source \"https://api.nuget.org/v3/index.json\" --skip-duplicate"

                match pushResult with
                | Error _ -> result <- 1
                | Ok _ -> ()

            return result
    }

type GithubRelease =
    {
        /// Is not suffixed with `v`
        Version: string
        Title: string
        Date: DateTime
        Draft: string
        Prerelease: bool
    }

let mapToGithubRelease (v: SemanticVersion, d: DateTime, cd: ChangelogData option) =
    match cd with
    | None -> failwith "Each release is expected to have at least one section."
    | Some cd ->

    let version = string v
    let title = $"v%s{version}"
    let prerelease = version.Contains("-")

    let sections =
        [
            "Added", cd.Added
            "Changed", cd.Changed
            "Fixed", cd.Fixed
            "Deprecated", cd.Deprecated
            "Removed", cd.Removed
            "Security", cd.Security
            yield! (Map.toList cd.Custom)
        ]
        |> List.choose (fun (header, lines) ->
            if String.IsNullOrWhiteSpace lines then
                None
            else
                lines
                |> _.TrimStart()
                |> sprintf "### %s\n%s" header
                |> Some
        )
        |> String.concat "\n\n"

    let draft =
        $"""# {version}

{sections}"""

    {
        Version = version
        Title = title
        Date = d
        Draft = draft
        Prerelease = prerelease
    }

let getReleaseNotes
    (ctx: CommandRunner)
    (currentRelease: GithubRelease)
    (previousReleaseDate: string option)
    =
    async {
        let closedFilter =
            match previousReleaseDate with
            | None -> ""
            | Some date -> $"closed:>%s{date}"

        let! authorsStdOut =
            ctx.RunCommandCaptureOutput
                $"gh pr list -S \"state:closed base:main %s{closedFilter} -author:app/robot -author:app/dependabot\" --json author --jq \".[].author.login\""

        let authorMsg =
            match authorsStdOut with
            | Error e -> failwithf $"Could not get authors: %s{e}"
            | Ok stdOut ->

            let authors =
                stdOut.Split([| '\n' |], StringSplitOptions.RemoveEmptyEntries)
                |> Array.distinct
                |> Array.sort

            if authors.Length = 1 then
                $"Special thanks to @%s{authors.[0]}!"
            else
                let lastAuthor = Array.last authors
                let otherAuthors =
                    if authors.Length = 2 then
                        $"@{authors.[0]}"
                    else
                        authors
                        |> Array.take (authors.Length - 1)
                        |> Array.map (sprintf "@%s")
                        |> String.concat ", "
                $"Special thanks to %s{otherAuthors} and @%s{lastAuthor}!"

        return
            $"""{currentRelease.Draft}

{authorMsg}

[https://www.nuget.org/packages/FSharp.Analyzers.SDK/{currentRelease.Version}](https://www.nuget.org/packages/FSharp.Analyzers.SDK/{currentRelease.Version})
[https://www.nuget.org/packages/FSharp.Analyzers.SDK.Testing/{currentRelease.Version}](https://www.nuget.org/packages/FSharp.Analyzers.SDK.Testing/{currentRelease.Version})
[https://www.nuget.org/packages/fsharp-analyzers/{currentRelease.Version}](https://www.nuget.org/packages/fsharp-analyzers/{currentRelease.Version})
    """
    }

/// Create a GitHub release via the CLI, with the *.nupkg files attached.
let mkGitHubRelease
    (ctx: CommandRunner)
    (currentVersion: SemanticVersion * DateTime * ChangelogData option)
    (previousReleaseDate: string option)
    =
    async {
        let ghReleaseInfo = mapToGithubRelease currentVersion
        let! notes = getReleaseNotes ctx ghReleaseInfo previousReleaseDate
        ctx.LogWhenDryRun $"NOTES:\n%s{notes}"
        let noteFile = Path.GetTempFileName()
        File.WriteAllText(noteFile, notes)
        let packages =
            getPackageFiles ghReleaseInfo.Version
            |> Array.map (sprintf "\"%s\"")
            |> String.concat " "

        let prereleaseFlag = if ghReleaseInfo.Prerelease then "--prerelease" else ""

        let! releaseResult =
            ctx.RunCommand
                $"gh release create v%s{ghReleaseInfo.Version} %s{packages} %s{prereleaseFlag} --title \"%s{ghReleaseInfo.Title}\" --notes-file \"%s{noteFile}\""

        if File.Exists noteFile then
            File.Delete(noteFile)

        match releaseResult with
        | Error _ -> return 1
        | Ok _ -> return 0
    }

pipeline "Release" {
    restoreStage
    buildStage
    packStage
    stage "publish" {
        run (fun ctx ->
            async {
                let commandRunner =
                    match ctx.TryGetCmdArg "--dry-run" with
                    | ValueNone ->
                        { new CommandRunner with
                            member x.LogWhenDryRun _ = ()
                            member x.RunCommand command = ctx.RunCommand command
                            member x.RunCommandCaptureOutput command =
                                ctx.RunCommandCaptureOutput command
                        }
                    | ValueSome _ ->
                        { new CommandRunner with
                            member x.LogWhenDryRun msg = printfn "%s" msg
                            member x.RunCommand command =
                                async {
                                    printfn $"[dry-run]:{command}"
                                    return Ok()
                                }
                            member x.RunCommandCaptureOutput command =
                                async {
                                    printfn $"[dry-run]:{command}"
                                    return Ok "nojaf\ndawedawe\nbaronfel"
                                }
                        }

                let currentVersion = getLatestChangeLogVersion ()
                let currentVersionText, _, _ = currentVersion
                let! latestNugetVersion =
                    getLatestPublishedNugetVersion "FSharp.Analyzers.SDK"
                    |> Async.AwaitTask
                match latestNugetVersion with
                | None ->
                    let! nugetResult =
                        releaseNuGetPackages commandRunner (string currentVersionText)
                    let! githubResult = mkGitHubRelease commandRunner currentVersion None
                    return nugetResult + githubResult

                | Some nugetVersion when
                    (nugetVersion.OriginalVersion
                     <> string currentVersionText)
                    ->
                    let! nugetResult =
                        releaseNuGetPackages commandRunner (string currentVersionText)
                    let! previousReleaseDate =
                        ctx.RunCommandCaptureOutput
                            $"gh release view v%s{nugetVersion.OriginalVersion} --json createdAt -t \"{{{{.createdAt}}}}\""

                    let previousReleaseDate =
                        match previousReleaseDate with
                        | Error e ->
                            printfn "Unable to format previous release data, %s" e
                            None
                        | Ok d ->
                            let output = d.Trim()
                            let lastIdx = output.LastIndexOf("Z", StringComparison.Ordinal)
                            Some(output.Substring(0, lastIdx))

                    let! githubResult =
                        mkGitHubRelease commandRunner currentVersion previousReleaseDate
                    return nugetResult + githubResult

                | Some nugetVersion ->
                    printfn "%s is already published" nugetVersion.OriginalVersion
                    return 0
            }
        )
    }
    runIfOnlySpecified true
}

tryPrintPipelineCommandHelp ()
