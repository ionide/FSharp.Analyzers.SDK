---
category: end-users
categoryindex: 1
index: 1
---

# Getting started using analyzers

## Premise

We assume the analyzers you want to use are distributed as a nuget package.

## Using analyzers in a single project

### Raw command line

A dotnet CLI tool, called [fsharp-analyzers](https://www.nuget.org/packages/fsharp-analyzers), is used to run analyzers outside the context of an IDE.  
Add it to your tool-manifest with:
```shell
dotnet tool install fsharp-analyzers
```

Next, add the `PackageReference` pointing to your favorite analyzers to the `.fsproj` file of the project you want to analyze:

```xml
<PackageReference Include="G-Research.FSharp.Analyzers" Version="0.4.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>analyzers</IncludeAssets>
</PackageReference>
```

At the time of writing, the [G-Research analyzers](https://github.com/g-research/fsharp-analyzers) [package](https://www.nuget.org/packages/G-Research.FSharp.Analyzers) contains the only analyzers compatible with the latest CLI tool.  
With the package downloaded, we can run the CLI tool:

```shell
dotnet fsharp-analyzers --project ./YourProject.fsproj --analyzers-path C:\Users\yourusername\.nuget\packages\g-research.fsharp.analyzers\0.4.0\analyzers\dotnet\fs\ --verbosity d
```

### Using an MSBuild target

As you can see, the path to the analyzer DLL files could be tricky to get right across a wide range of setups.  
Luckily, we can use an MSBuild custom target to take care of the path construction.  

Note: If you're using FAKE you can [call the analyzers from a FAKE build script](#Call-Analyzers-in-Your-FAKE-Build).

Add [FSharp.Analyzers.Build](https://www.nuget.org/packages/FSharp.Analyzers.Build) to your `fsproj`:

```xml
<PackageReference Include="FSharp.Analyzers.Build" Version="0.2.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>build</IncludeAssets>
</PackageReference>
```

This imports a new target to your project file: `AnalyzeFSharpProject`.  
And will allow us to easily run the analyzers for our project.  

Before we can run `dotnet msbuild /t:AnalyzeFSharpProject`, we need to specify our settings in a property called `FSharpAnalyzersOtherFlags`:

```xml
<PropertyGroup>
    <FSharpAnalyzersOtherFlags>--analyzers-path &quot;$(PkgG-Research_FSharp_Analyzers)/analyzers/dotnet/fs&quot; --report &quot;$(MSBuildProjectName)-$(TargetFramework).sarif&quot; --treat-as-warning IONIDE-004 --verbosity d</FSharpAnalyzersOtherFlags>
</PropertyGroup>
```

To locate the analyzer DLLs in the filesystem, we use the variable `$(PkgG-Research_FSharp_Analyzers)`. It's produced by NuGet and normalized to be usable by [MSBuild](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#generatepathproperty).
In general, a `Pkg` prefix is added and dots in the package ID are replaced by underscores. But make sure to look at the [nuget.g.props](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#restore-outputs) file in the `obj` folder for the exact string.  
The `/analyzers/dotnet/fs` subpath is a convention analyzer authors should follow when creating their packages.

At last, you can run the analyzer from the project folder:

```shell
dotnet msbuild /t:AnalyzeFSharpProject
```

Note: if your project has multiple `TargetFrameworks` the tool will be invoked for each target framework.

## Using analyzers in a solution

Adding the custom target from above to all `.fsproj` files of a solution doesn't scale very well.  
So we use the MSBuild infrastructure to add the needed package reference and the MSBuild target to all projects in one go.  

We start with adding the `PackageReference` pointing to your favorite analyzers to the [Directory.Build.props](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory?view=vs-2022) file.  
This adds the package reference to all `.fsproj` files that are in a subfolder of the file location of `Directory.Build.props`:

```xml
<ItemGroup>
    <PackageReference Include="FSharp.Analyzers.Build" Version="0.2.0">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>build</IncludeAssets>
    </PackageReference>
    <PackageReference Include="G-Research.FSharp.Analyzers" Version="0.1.6">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>analyzers</IncludeAssets>
    </PackageReference>
</ItemGroup>
```

Likewise we add the `FSharpAnalyzersOtherFlags` property to the [Directory.Build.targets](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory?view=vs-2022) file.  
This is effectively the same as adding a property to each `*proj` file which exists in a subfolder.
```xml
<Project>
    <PropertyGroup>
        <SarifOutput Condition="$(SarifOutput) == ''">./</SarifOutput>
        <CodeRoot Condition="$(CodeRoot) == ''">.</CodeRoot>
        <FSharpAnalyzersOtherFlags>--analyzers-path &quot;$(PkgG-Research_FSharp_Analyzers)/analyzers/dotnet/fs&quot; --report &quot;$(SarifOutput)$(MSBuildProjectName)-$(TargetFramework).sarif&quot; --code-root $(CodeRoot) --treat-as-warning IONIDE-004 --verbosity d</FSharpAnalyzersOtherFlags>
    </PropertyGroup>
</Project>
```

⚠️ We are adding the `FSharpAnalyzersOtherFlags` property to our **Directory.Build.targets** and **not to** any **Directory.Build.props** file!  
MSBuild will first evaluate `Directory.Build.props` which has no access to the generated nuget.g.props. `$(PkgG-Research_FSharp_Analyzers)` won't be known at this point. `Directory.Build.targets` is evaluated after the project file and has access to `Pkg` generated properties.

### All projects in the solution

We can run the `AnalyzeFSharpProject` target against all projects in a solution

```shell
dotnet msbuild YourSolution.sln /t:AnalyzeFSharpProject
```

### Select specific projects

As we don't want to target all projects in the solution, we create a second custom MSBuild target that calls the project-specific target for all relevant projects.  
Add the following custom target to the [Directory.Solution.targets](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-solution-build?view=vs-2022) file to be able to invoke analysis from all selected projects in one simple command:

```xml
<Project>
    <ItemGroup>
        <ProjectsToAnalyze Include="src/**/*.fsproj" />
    </ItemGroup>

    <Target Name="AnalyzeSolution">
        <MSBuild Projects="@(ProjectsToAnalyze)" Targets="AnalyzeFSharpProject" />
    </Target>
</Project>
```

At last, you can run the analyzer from the solution folder:

```shell
dotnet msbuild /t:AnalyzeSolution
```

Note: we passed the `--code-root` flag so that the `*.sarif` report files will report file paths relative to this root. This can be imported for certain editors to function properly. 

## MSBuild tips and tricks

MSBuild can be overwhelming for the uninitiated. Here are some tricks we've seen in the wild:

### Use well-known properties

Checkout the [MSBuild reserved and well-known properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-reserved-and-well-known-properties?view=vs-2022) to use existing variables like `$(MSBuildProjectFile)`.

### Wrap path arguments in quotes

As MSBuild is all XML, you can use `&quot;` to wrap evaluated values in quotes:

```xml
<PropertyGroup>
    <WithQuotes>&quot;$(SolutionDir)&quot;</WithQuotes>
</PropertyGroup>
```

### Extend `<FSharpAnalyzersOtherFlags>` in multiple lines

You can extend the value of `$(FSharpAnalyzersOtherFlags)` by setting it again in multiple lines:

```xml
<PropertyGroup>
    <FSharpAnalyzersOtherFlags>--analyzers-path &quot;$(PkgG-Research_FSharp_Analyzers)/analyzers/dotnet/fs&quot;</FSharpAnalyzersOtherFlags>
    <FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --analyzers-path &quot;$(PkgIonide_Analyzers)/analyzers/dotnet/fs&quot;</FSharpAnalyzersOtherFlags>
    <FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --configuration $(Configuration)</FSharpAnalyzersOtherFlags>
    <FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --exclude-analyzers PartialAppAnalyzer</FSharpAnalyzersOtherFlags>
</PropertyGroup>
```

### Verify parameters are present

It can be a bit confusing to find out if a variable contains the value you think it does.
We often add a dummy target to a project to print out some values:

```xml
<Target Name="Dump">
    <Message Importance="high" Text="$(CodeRoot)" />
</Target>
```

Run `dotnet msbuild YourProject.fsproj /t:Dump` and verify that `CodeRoot` has a value or not.

## Call Analyzers in Your FAKE Build

The below example assumes:

1. You have the `fsharp-analyzers` dotnet tool installed
2. You are using [FAKE](https://fake.build/) as your build automation
3. You are using [Paket](https://github.com/fsprojects/Paket) as your package manager

You can adapt this example to work with other build automation tools and package managers.

```fsharp
open Fake.Api
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open System.IO

let restore _ =
    // this is a dummy example of how you can restore your solution
    let setParams : DotNet.RestoreOptions -> DotNet.RestoreOptions = id
    DotNet.restore setParams "MySolution.sln"

let runAnalyzers args = DotNet.exec id "fsharp-analyzers" args

let analyze _ =
    // this example is using paket as our package manager & we have our analyzers in a group called "analyzers"
    // however you can grab your analyzers from anywhere
    let analyzerPaths = !! "packages/analyzers/**/analyzers/dotnet/fs"

    let createArgsForProject (project: string) analyzerPaths =
        [
            "--project"
            project
            "--analyzers-path"
            yield! analyzerPaths
        ]
        |> String.concat " "

    // use globbing to get all the fsproj files you want to analyze
    !! "src/**/*.fsproj"
    |> Seq.iter (fun fsproj ->
        let result =
            createArgsForProject fsproj analyzerPaths
            |> runAnalyzers

        result.Errors
        |> Seq.iter Trace.traceError
    )

// other FAKE code here...

Target.create "Restore" restore
Target.create "Analyzers" analyze

// example of setting up analyzers in your dependency graph
"Restore" ==> "Analyzers" |> ignore
```

[Next]({{fsdocs-next-page-link}})