---
category: getting-started
categoryindex: 1
index: 4
---

# MSBuild

## Using Analyzer Build Target in a Project

The path to the analyzer DLL files could be tricky to get right across a wide range of setups. Luckily, we can use a MSBuild custom target to take care of the path construction. Add [FSharp.Analyzers.Build](https://www.nuget.org/packages/FSharp.Analyzers.Build) to your project. This imports a new target to your project file (`AnalyzeFSharpProject`) and will allow us to easily run the analyzers for our project.   

### Installing Target via Nuget

If you are using Nuget, add it to your `.fsproj` file:

```xml
<PackageReference Include="FSharp.Analyzers.Build" Version="0.2.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>build</IncludeAssets>
</PackageReference>
```

### Installing Target via Paket

If you are using Paket, add it to your `paket.dependencies`

```paket
group analyzers
    source https://api.nuget.org/v3/index.json

    nuget FSharp.Analyzers.Build
```

as well as the `paket.references` of your project:

```paket
group analyzers
  FSharp.Analyzers.Build
```

### Configuring the Build Target

Before we can run `dotnet msbuild /t:AnalyzeFSharpProject`, we need to specify our settings in a property called `FSharpAnalyzersOtherFlags`:

```xml
<PropertyGroup>
    <FSharpAnalyzersOtherFlags>--analyzers-path &quot;$(PkgG-Research_FSharp_Analyzers)/analyzers/dotnet/fs&quot; --report &quot;$(MSBuildProjectName)-$(TargetFramework).sarif&quot; --treat-as-warning IONIDE-004 --verbosity d</FSharpAnalyzersOtherFlags>
</PropertyGroup>
```

To locate the analyzer DLLs in the filesystem, we use the variable `$(PkgG-Research_FSharp_Analyzers)`. It's produced by NuGet and normalized to be usable by [MSBuild](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#generatepathproperty).
In general, a `Pkg` prefix is added and dots in the package ID are replaced by underscores. But make sure to look at the [nuget.g.props](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#restore-outputs) file in the `obj` folder for the exact string.

The `/analyzers/dotnet/fs` subpath is a convention analyzer authors should follow when creating their packages.

### Running the Build Target

At last, you can run the analyzer from the project folder:

```shell
dotnet msbuild /t:AnalyzeFSharpProject
```

📓 Note: If your project has multiple `TargetFrameworks` the tool will be invoked for each target framework.

## Using Analyzer Build Target in a Solution

Adding the custom target from above to all `.fsproj` files of a solution doesn't scale very well. We can use the MSBuild infrastructure to add the needed package reference and the MSBuild target to all projects in one go.

### Setting up Directory.Build.props

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

### Setting up Directory.Build.targets

Likewise we add the `FSharpAnalyzersOtherFlags` property to the [Directory.Build.targets](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory?view=vs-2022) file. For first time MSBuild users, this is effectively the same as adding a property to each `*proj` file which exists in a subfolder.

```xml
<Project>
    <PropertyGroup>
        <SarifOutput Condition="$(SarifOutput) == ''">./</SarifOutput>
        <CodeRoot Condition="$(CodeRoot) == ''">.</CodeRoot>
        <FSharpAnalyzersOtherFlags>--analyzers-path &quot;$(PkgG-Research_FSharp_Analyzers)/analyzers/dotnet/fs&quot; --report &quot;$(SarifOutput)$(MSBuildProjectName)-$(TargetFramework).sarif&quot; --code-root $(CodeRoot) --treat-as-warning IONIDE-004 --verbosity d</FSharpAnalyzersOtherFlags>
    </PropertyGroup>
</Project>
```

⚠️ We are adding the `FSharpAnalyzersOtherFlags` property to our **Directory.Build.targets** and **not to** any **Directory.Build.props** file! MSBuild will first evaluate `Directory.Build.props` which has no access to the generated nuget.g.props. `$(PkgG-Research_FSharp_Analyzers)` won't be known at this point. `Directory.Build.targets` is evaluated after the project file and has access to `Pkg` generated properties.

### Run Target for All Projects in the Solution

We can run the `AnalyzeFSharpProject` target against all projects in a solution

```shell
dotnet msbuild YourSolution.sln /t:AnalyzeFSharpProject
```

### Configuring Specific Projects to Run

As we may not always want to target every project in a solution, we can   create a second custom MSBuild target that calls the project-specific target for all relevant projects.  
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

You can also exclude certain projects from the analysis if they fall within the same pattern

```xml
<Project>
    <ItemGroup>
      <ProjectsToAnalyze Include="src/**/*.fsproj" Exclude="src/**/Special.fsproj" />
    </ItemGroup>
    
    <Target Name="AnalyzeSolution">
        <MSBuild Projects="@(ProjectsToAnalyze)" Targets="AnalyzeFSharpProject" />
    </Target>
</Project>
```

### Running the Solution Target

At last, you can run the analyzer from the solution folder:

```shell
dotnet msbuild /t:AnalyzeSolution
```

Note: we passed the `--code-root` flag so that the `*.sarif` report files will report file paths relative to this root. This can be imported for certain editors to function properly. 

## MSBuild Tips and Tricks

MSBuild can be overwhelming for the uninitiated. Here are some tricks we've seen in the wild:

### Use Well-Known Properties

Checkout the [MSBuild reserved and well-known properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-reserved-and-well-known-properties?view=vs-2022) to use existing variables like `$(MSBuildProjectFile)`.

### Wrap Path Arguments in Quotes

As MSBuild is all XML, you can use `&quot;` to wrap evaluated values in quotes:

```xml
<PropertyGroup>
    <WithQuotes>&quot;$(SolutionDir)&quot;</WithQuotes>
</PropertyGroup>
```

### Extend `<FSharpAnalyzersOtherFlags>` in Multiple Lines

You can extend the value of `$(FSharpAnalyzersOtherFlags)` by setting it again in multiple lines:

```xml
<PropertyGroup>
    <FSharpAnalyzersOtherFlags>--analyzers-path &quot;$(PkgG-Research_FSharp_Analyzers)/analyzers/dotnet/fs&quot;</FSharpAnalyzersOtherFlags>
    <FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --analyzers-path &quot;$(PkgIonide_Analyzers)/analyzers/dotnet/fs&quot;</FSharpAnalyzersOtherFlags>
    <FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --configuration $(Configuration)</FSharpAnalyzersOtherFlags>
    <FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --exclude-analyzers PartialAppAnalyzer</FSharpAnalyzersOtherFlags>
</PropertyGroup>
```

### Verify Parameters are Present

It can be a bit confusing to find out if a variable contains the value you think it does.
We often add a dummy target to a project to print out some values:

```xml
<Target Name="Dump">
    <Message Importance="high" Text="$(CodeRoot)" />
</Target>
```

Run `dotnet msbuild YourProject.fsproj /t:Dump` and verify that `CodeRoot` has a value or not.

[Next]({{fsdocs-next-page-link}})