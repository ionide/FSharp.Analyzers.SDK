#r "paket: groupref build //"
#load ".fake/build.fsx/intellisense.fsx"
#if !FAKE
  #r "netstandard"
  #r "Facades/netstandard.dll"
#endif

open Fake.Core
open Fake.DotNet
open Fake.Tools
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Api


// --------------------------------------------------------------------------------------
// Information about the project to be used at NuGet and in AssemblyInfo files
// --------------------------------------------------------------------------------------

let project = "FSharp.Analyzers.SDK"
let summary = "SDK for building custom analyzers for FSAC / F# editors"
let gitOwner = "Krzysztof-Cieslak"
let gitName = "FSharp.Analyzers.SDK"
let gitHome = "https://github.com/" + gitOwner

// --------------------------------------------------------------------------------------
// Build variables
// --------------------------------------------------------------------------------------

let buildDir = "out"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let release = ReleaseNotes.parse (System.IO.File.ReadAllLines "RELEASE_NOTES.md")

// --------------------------------------------------------------------------------------
// Helpers
// --------------------------------------------------------------------------------------

let isNullOrWhiteSpace = System.String.IsNullOrWhiteSpace

let getBuildParam = Environment.environVar

let DoNothing = ignore

// --------------------------------------------------------------------------------------
// Build Targets
// --------------------------------------------------------------------------------------

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    ++ "out"
    |> Shell.cleanDirs
)

Target.create "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ AssemblyInfo.Title projectName
          AssemblyInfo.Product project
          AssemblyInfo.Description summary
          AssemblyInfo.Version release.AssemblyVersion
          AssemblyInfo.FileVersion release.AssemblyVersion ]

    let getProjectDetails (projectPath: string) =
        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          System.IO.Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    !! "src/**/*.??proj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, _, folderName, attributes) ->
        match projFileName with
        | proj when proj.EndsWith("fsproj") -> AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") attributes
        | proj when proj.EndsWith("csproj") -> AssemblyInfoFile.createCSharp ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
        | proj when proj.EndsWith("vbproj") -> AssemblyInfoFile.createVisualBasic ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
        | _ -> ()
        )
)

Target.create "Restore" (fun _ ->
    DotNet.restore id ""
)

Target.create "Build" (fun _ ->
    DotNet.build (fun p ->
        { p with
            Configuration = DotNet.BuildConfiguration.Release;
            MSBuildParams = { MSBuild.CliArguments.Create() with
                                Properties = ["Version", release.NugetVersion] }
        }
    ) ""
)


// --------------------------------------------------------------------------------------
// Release Targets
// --------------------------------------------------------------------------------------

Target.create "Pack" (fun _ ->
    DotNet.pack (fun p ->
        { p with
            Configuration = DotNet.BuildConfiguration.Release;
            OutputPath = Some buildDir
            MSBuildParams = { MSBuild.CliArguments.Create() with
                                Properties = ["Version", release.NugetVersion
                                              "PackageReleaseNotes", String.concat "\n" release.Notes] }
        }
    ) ""
)

Target.create "ReleaseGitHub" (fun _ ->
    let remote =
        Git.CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (fun (s: string) -> s.EndsWith("(push)"))
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s" release.NugetVersion)
    Git.Branches.pushBranch "" remote (Git.Information.getBranchName "")


    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" remote release.NugetVersion

    let client =
        let token =
            match getBuildParam "github-token" with
            | s when not (isNullOrWhiteSpace s) -> s
            | _ -> UserInput.getUserInput "github API token: "

        GitHub.createClientWithToken token
    let files = !! (buildDir </> "*.nupkg")

    // release on github
    let cl =
        client
        |> GitHub.draftNewRelease gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    (cl,files)
    ||> Seq.fold (fun acc e -> acc |> GitHub.uploadFile e)
    |> GitHub.publishDraft//releaseDraft
    |> Async.RunSynchronously
)

Target.create "Push" (fun _ ->
    let key =
        match getBuildParam "nuget-key" with
        | s when not (isNullOrWhiteSpace s) -> s
        | _ -> UserInput.getUserPassword "NuGet Key: "
    Paket.push (fun p -> { p with WorkingDir = buildDir; ApiKey = key }))

Target.create "RunSample" (fun _ ->
    let args = @"--project src\FSharp.Analyzers.Cli\FSharp.Analyzers.Cli.fsproj -- --project ./samples/OptionAnalyzer/OptionAnalyzer.fsproj --analyzers-path ./samples/OptionAnalyzer/bin/Release --verbose"

    DotNet.exec id "run" args
    |> ignore
)

// --------------------------------------------------------------------------------------
// Build order
// --------------------------------------------------------------------------------------

Target.create "Default" DoNothing
Target.create "Release" DoNothing

"Clean"
  ==> "AssemblyInfo"
  ==> "Restore"
  ==> "Build"
  ==> "Default"

"Default"
  ==> "Pack"
  ==> "ReleaseGitHub"
  ==> "Push"
  ==> "Release"

"Build"
  ==> "RunSample"


Target.runOrDefault "Default"
