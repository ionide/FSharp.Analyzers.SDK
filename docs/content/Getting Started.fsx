(**
---
category: end-users
categoryindex: 1
index: 1
---

# Getting started

## Premise

Analyzers that are consumed by this SDK and from Ionide are simply .NET core class libraries.  
These class libraries expose a *value* of type [Analyzer<'TContext>](../reference/fsharp-analyzers-sdk-analyzer-1.html) which is effectively a function that has input of type [Context](../reference/fsharp-analyzers-sdk-context.html) and returns a list of [Message](../reference/fsharp-analyzers-sdk-message.html) records.

## Create project

Create a new class library targeting `net6.0`

```shell
dotnet new classlib -lang F# -f net6.0 -n OptionValueAnalyzer
```

Note that the assembly name needs to contain `Analyzer` in the name in order for it to be picked up.

Add a reference to the analyzers SDK:

```shell
dotnet add package FSharp.Analyzers.SDK
```

⚠️ Note: To utilize the analyzers in FsAutoComplete (which is subsequently utilized by Ionide), it is essential to ensure that the SDK version matches correctly.

```shell
paket add FSharp.Analyzers.SDK
```

## First analyzer

An [Analyzer<'TContext>](../reference/fsharp-analyzers-sdk-analyzer-1.html) is a function that takes a `Context` and returns a list of `Message`.  
There are two flavours of analyzers:

- Console application analyzers ([CliAnalyzer](../reference/fsharp-analyzers-sdk-clianalyzerattribute.html))
- Editor analyzers ([EditorAnalyzer](../reference/fsharp-analyzers-sdk-editoranalyzerattribute.html))

The key difference between them is that the console application analyzer will have the *full project* information.  
Per file this includes the untyped tree, typed tree, type-check results of the file and project type-check results.  
The [fsharp-analyzers](https://www.nuget.org/packages/fsharp-analyzers) tool will collect all this information upfront and pass it down to the analyzer via the [CliContext](../reference/fsharp-analyzers-sdk-clicontext.html).

In the case of an editor analyzer, the IDE might not have all the available information available and will be more selective in what it can pass down to the analyzer.
The main reasoning behind this is performance. It might be desirable for some analyzers to run after every keystroke, while others should be executed more sparingly.

In the following example we will be 
*)

(*** hide ***)
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Analyzers.SDK.dll"
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Compiler.Service.dll"
(** *)
open FSharp.Analyzers.SDK

// This attribute is required and needs to match the correct context type!
[<CliAnalyzer>]
let optionValueAnalyzer: Analyzer<CliContext> =
    fun (context: CliContext) ->
        async {
            // inspect context to determine the error/warning messages
            // A potential implementation might traverse the untyped syntax tree
            // to find any references of `Option.Value`
            return
                [
                    {
                        Type = "Option.Value analyzer"
                        Message = "Option.Value shouldn't be used"
                        Code = "OV001"
                        Severity = Warning
                        Range = FSharp.Compiler.Text.Range.Zero
                        Fixes = []
                    }
                ]
        }

(**
Analyzers can also be named which allows for better logging if something went wrong while using the SDK from Ionide:
*)

[<EditorAnalyzer "BadCodeAnalyzer">]
let badCodeAnalyzer: Analyzer<EditorContext> =
    fun (context: EditorContext) ->
        async { // inspect context to determine the error/warning messages
            return []
        }

(**
## Running your first analyzer

After building your project you can run your analyzer on a project of your choosing using the [fsharp-analyzers](https://www.nuget.org/packages/fsharp-analyzers) tool.  
Again, please verify your analyzer is a `CliAnalyzerAttribute` and uses the `CliContext`!

```shell
dotnet tool install --global fsharp-analyzers
```

```shell
fsharp-analyzers --project YourProject.fsproj --analyzers-path ./OptionAnalyzer/bin/Release --verbose
```

### Packaging and Distribution

Since analyzers are just .NET core libraries, you can distribute them to the nuget registry just like you would with a normal .NET package.  
Simply run `dotnet pack --configuration Release` against the analyzer project to get a nuget package and publish it with

```shell
dotnet nuget push {NugetPackageFullPath} -s nuget.org -k {NugetApiKey}
```

However, the story is different and slightly more complicated when your analyzer package has third-party dependencies also coming from nuget. Since the SDK dynamically loads the package assemblies (`.dll` files), the assemblies of the dependencies has be there *next* to the main assembly of the analyzer. Using `dotnet pack` will **not** include these dependencies into the output Nuget package. More specifically, the `./lib/net6.0` directory of the nuget package must have all the required assemblies, also those from third-party packages. In order to package the analyzer properly with all the assemblies, you need to take the output you get from running:

```shell
dotnet publish --configuration Release --framework net6.0
```

against the analyzer project and put every file from that output into the `./lib/net6.0` directory of the nuget package. This requires some manual work by unzipping the nuget package first (because it is just an archive), modifying the directories then zipping the package again. It can be done using a FAKE build target to automate the work:
*)

// make ZipFile available
#r "System.IO.Compression.FileSystem.dll"
#r "nuget: Fake.Core.Target, 6.0.0"
#r "nuget: Fake.Core.ReleaseNotes, 6.0.0"
#r "nuget: Fake.IO.Zip, 6.0.0"

open System.IO
open System.IO.Compression
open Fake.Core
open Fake.IO
open Fake.IO.FileSystemOperators

let releaseNotes = ReleaseNotes.load "RELEASE_NOTES.md"

Target.create
    "PackAnalyzer"
    (fun _ ->
        let analyzerProject = "src" </> "BadCodeAnalyzer"

        let args =
            [
                "pack"
                "--configuration Release"
                sprintf "/p:PackageVersion=%s" releaseNotes.NugetVersion
                sprintf "/p:PackageReleaseNotes=\"%s\"" (String.concat "\n" releaseNotes.Notes)
                sprintf "--output %s" (__SOURCE_DIRECTORY__ </> "dist")
            ]

        // create initial nuget package
        let exitCode = Shell.Exec("dotnet", String.concat " " args, analyzerProject)

        if exitCode <> 0 then
            failwith "dotnet pack failed"
        else
            match Shell.Exec("dotnet", "publish --configuration Release --framework net6.0", analyzerProject) with
            | 0 ->
                let nupkg =
                    System.IO.Directory.GetFiles(__SOURCE_DIRECTORY__ </> "dist")
                    |> Seq.head
                    |> Path.GetFullPath

                let nugetParent = DirectoryInfo(nupkg).Parent.FullName
                let nugetFileName = Path.GetFileNameWithoutExtension(nupkg)

                let publishPath = analyzerProject </> "bin" </> "Release" </> "net6.0" </> "publish"
                // Unzip the nuget
                ZipFile.ExtractToDirectory(nupkg, nugetParent </> nugetFileName)
                // delete the initial nuget package
                File.Delete nupkg
                // remove stuff from ./lib/net6.0
                Shell.deleteDir (nugetParent </> nugetFileName </> "lib" </> "net6.0")
                // move the output of publish folder into the ./lib/net6.0 directory
                Shell.copyDir (nugetParent </> nugetFileName </> "lib" </> "net6.0") publishPath (fun _ -> true)
                // re-create the nuget package
                ZipFile.CreateFromDirectory(nugetParent </> nugetFileName, nupkg)
                // delete intermediate directory
                Shell.deleteDir (nugetParent </> nugetFileName)
            | _ -> failwith "dotnet publish failed"
    )

(**

[Next]({{fsdocs-next-page-link}})

*)
