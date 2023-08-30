# FSharp.Analyzers.SDK

Library used for building custom analyzers for FSAC / F# editors.

F# analyzers are live, real-time, project based plugins that enables to diagnose source code and surface custom errors, warnings and code fixes into editor. Read more about analyzers here - https://medium.com/lambda-factory/introducing-f-analyzers-772487889429

## How to build

1. Install the .NET SDK version specified in `global.json`
2. `dotnet tool restore`
2. Open and build in your favorite IDE, or use `dotnet build`

## How to run sample
1. `dotnet build -c Release`
2.
```shell
dotnet run --project src\FSharp.Analyzers.Cli\FSharp.Analyzers.Cli.fsproj -- --project ./samples/OptionAnalyzer/OptionAnalyzer.fsproj --analyzers-path ./samples/OptionAnalyzer/bin/Release --verbose
```


You can also set up a run configuration of FSharp.Analyzers.Cli in your favorite IDE using similar arguments. This also allows you to debug FSharp.Analyzers.Cli.

## Writing Analyzers

Analyzers that are consumed by this SDK and from Ionide are simply .NET core class libraries. These class libraries expose a *value* of type `Analyzer` which is effectively a function that has input of type `Context` and returns a list of `Message` records:
```fsharp
module BadCodeAnalyzer

open FSharp.Analyzers.SDK

[<Analyzer>]
let badCodeAnalyzer : Analyzer =
  fun (context: Context) ->
    // inspect context to determine the error/warning messages
    [   ]
```
Notice how we expose the function `BadCodeAnalyzer.badCodeAnalyzer` with an attribute `[<Analyzer>]` that allows the SDK to detect the function. The input `Context` is a record that contains information about a single F# file such as the typed AST, the AST, the file content, the file name and more. The SDK runs this function against all files of a project during editing. The output messages that come out of the function are eventually used by Ionide to highlight the inspected code as a warning or error depending on the `Severity` level of each message.

Analyzers can also be named which allows for better logging if something went wrong while using the SDK from Ionide:
```fs
[<Analyzer "BadCodeAnalyzer">]
let badCodeAnalyzer : Analyzer =
  fun (context: Context) ->
    // inspect context to determine the error/warning messages
    [   ]
```
### Analyzer Requirements

Analyzers are .NET core class libraries and they are distributed as such. However, since the SDK relies on dynamically loading the analyzers during runtime, there are some requirements to get them to work properly:
 - The analyzer class library has to target the `net6.0` framework
 - The analyzer has to reference the latest `FSharp.Analyzers.SDK` (at least the version used by FsAutoComplete which is subsequently used by Ionide)

### Packaging and Distribution

Since analyzers are just .NET core libraries, you can distribute them to the nuget registry just like you would with a normal .NET package. Simply run `dotnet pack --configuration Release` against the analyzer project to get a nuget package and publish it with

```
dotnet nuget push {NugetPackageFullPath} -s nuget.org -k {NugetApiKey}
```

However, the story is different and slightly more complicated when your analyzer package has third-party dependencies also coming from nuget. Since the SDK dynamically loads the package assemblies (`.dll` files), the assemblies of the dependencies has be there *next* to the main assembly of the analyzer. Using `dotnet pack` will **not** include these dependencies into the output Nuget package. More specifically, the `./lib/net6.0` directory of the nuget package must have all the required assemblies, also those from third-party packages. In order to package the analyzer properly with all the assemblies, you need to take the output you get from running:
```
dotnet publish --configuration Release --framework net6.0
```
against the analyzer project and put every file from that output into the `./lib/net6.0` directory of the nuget package. This requires some manual work by unzipping the nuget package first (because it is just an archive), modifying the directories then zipping the package again. It can be done using a FAKE build target to automate the work:
```fs
// make ZipFile available
#r "System.IO.Compression.FileSystem.dll"

let releaseNotes = ReleaseNotes.load "RELEASE_NOTES.md"

Target.create "PackAnalyzer" (fun _ ->
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
                |> IO.Path.GetFullPath

            let nugetParent = DirectoryInfo(nupkg).Parent.FullName
            let nugetFileName = IO.Path.GetFileNameWithoutExtension(nupkg)

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
            Shell.deleteDir(nugetParent </> nugetFileName)
        | _ ->
            failwith "dotnet publish failed"
)
```

## How to contribute

*Imposter syndrome disclaimer*: I want your help. No really, I do.

There might be a little voice inside that tells you you're not ready; that you need to do one more tutorial, or learn another framework, or write a few more blog posts before you can help me with this project.

I assure you, that's not the case.

This project has some clear Contribution Guidelines and expectations that you can [read here](https://github.com/Krzysztof-Cieslak/FSharp.Analyzers.SDK/blob/master/CONTRIBUTING.md).

The contribution guidelines outline the process that you'll need to follow to get a patch merged. By making expectations and process explicit, I hope it will make it easier for you to contribute.

And you don't just have to write code. You can help out by writing documentation, tests, or even by giving feedback about this work. (And yes, that includes giving feedback about the contribution guidelines.)

Thank you for contributing!


## Contributing and copyright

The project is hosted on [GitHub](https://github.com/Krzysztof-Cieslak/FSharp.Analyzers.SDK) where you can [report issues](https://github.com/Krzysztof-Cieslak/FSharp.Analyzers.SDK/issues), fork
the project and submit pull requests.

The library is available under [MIT license](https://github.com/Krzysztof-Cieslak/FSharp.Analyzers.SDK/blob/master/LICENSE.md), which allows modification and redistribution for both commercial and non-commercial purposes.
