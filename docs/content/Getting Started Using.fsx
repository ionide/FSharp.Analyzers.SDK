(**
---
category: end-users
categoryindex: 1
index: 1
---

# Getting started using analyzers

## Premise

We assume the analyzers you want to use are distributed as a nuget package.

## Using analyzers in a single project

A dotnet CLI tool, called [fsharp-analyzers](https://www.nuget.org/packages/fsharp-analyzers), is used to run analyzers outside the context of an IDE.  
Add it to your tool-manifest with:
```shell
dotnet tool install fsharp-analyzers
```

Next, add the `PackageReference` pointing to your favorite analyzers to the `.fsproj` file of the project you want to analyze:

```xml
<PackageReference Include="G-Research.FSharp.Analyzers" Version="0.1.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>build</IncludeAssets>
</PackageReference>
```

At the time of writing, the [G-Research analyzers](https://github.com/g-research/fsharp-analyzers) [package](https://www.nuget.org/packages/G-Research.FSharp.Analyzers) contains the only analyzers compatible with the latest CLI tool.  
With the package downloaded, we can run the CLI tool:

```shell
dotnet fsharp-analyzers --project ./YourProject.fsproj --analyzers-path C:\Users\yourusername\.nuget\packages\g-research.fsharp.analyzers\0.1.6\analyzers\dotnet\fs\ --verbose
```

As you can see, the path to the analyzer DLL files could be tricky to get right across a wide range of setups.  
Luckily, we can use an MSBuild custom target to take care of the path construction.  
Add the following target to the `.fsproj` file for easy invocation of the analyzer:

```xml
<Target Name="AnalyzeProject">

    <Message Importance="High" Text="Analyzing $(MSBuildProjectFile)"/>
    <Exec
        ContinueOnError="true"
        Command="dotnet fsharp-analyzers --project &quot;$(MSBuildProjectFile)&quot; --analyzers-path &quot;$(PkgG-Research_FSharp_Analyzers)\analyzers\dotnet\fs&quot; --exclude-analyzer PartialAppAnalyzer --fail-on-warnings GRA-STRING-001 --verbose --report &quot;$(MSBuildProjectName)-analysis.sarif&quot;">
        <Output TaskParameter="ExitCode" PropertyName="LastExitCode" />
    </Exec>
    <Error Condition="'$(LastExitCode)' == '-2'" Text="Problems were found $(MSBuildProjectFile)" />
</Target>
```

You may need to adjust the `Command` to be compatible with your specific analyzer. Think about how you want warnings to be treated.  

To locate the analyzer DLLs in the filesystem, we use the variable `$(PkgG-Research_FSharp_Analyzers)`. It's produced by NuGet and normalized to be usable by [MSBuild](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#generatepathproperty).
In general, a `Pkg` prefix is added and dots in the package ID are replaced by underscores. But make sure to look at the [nuget.g.props](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#restore-outputs) file in the `obj` folder for the exact string.  
The `\analyzers\dotnet\fs` subpath is a convention analyzer authors should follow when creating their packages.

At last, you can run the analyzer from the project folder:

```shell
dotnet msbuild /t:AnalyzeProject
```

## Using analyzers in a solution

Adding the custom target from above to all `.fsproj` files of a solution doesn't scale very well.  
So we use the MSBuild infrastructure to add the needed package reference and the MSBuild target to all projects in one go.  

We start with adding the `PackageReference` pointing to your favorite analyzers to the [Directory.Build.props](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory?view=vs-2022) file.  
This adds the package reference to all `.fsproj` files that are in a subfolder of the file location of `Directory.Build.props`:

```xml
<ItemGroup>
    <PackageReference Include="G-Research.FSharp.Analyzers" Version="0.1.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>build</IncludeAssets>
    </PackageReference>
</ItemGroup>
```

Likewise we add the following custom target to the [Directory.Build.targets](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory?view=vs-2022) file.  
This is effectively the same as adding a target to each `*proj` file which exists in a subfolder.
```xml
<Project>

    <Target
        Name="AnalyzeProject">
        
        <Message Importance="normal" Text="fsc arguments: @(FscCommandLineArgs)" />
        <Message Importance="High" Text="Analyzing $(MSBuildProjectFile)"/>
        <Exec
            ContinueOnError="true"
            Command="dotnet fsharp-analyzers --project &quot;$(MSBuildProjectFile)&quot; --analyzers-path &quot;$(PkgG-Research_FSharp_Analyzers)\analyzers\dotnet\fs&quot; --exclude-analyzer PartialAppAnalyzer --fail-on-warnings GRA-STRING-001 --verbose --report &quot;$(MSBuildProjectName)-analysis.sarif&quot;">
            <Output TaskParameter="ExitCode" PropertyName="LastExitCode" />
        </Exec>
        <Error Condition="'$(LastExitCode)' == '-2'" Text="Problems were found $(MSBuildProjectFile)" />

    </Target>

</Project>
```

You may need to adjust the `Command` to be compatible with your specific analyzer. Think about how you want warnings to be treated.

As we don't want to list all projects of the solution explicitly when analyzing the solution, we create a second cutom MSBuild target that calls the project-specific target for all projects.  
Add the following custom target to the [Directory.Solution.targets](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-solution-build?view=vs-2022) file to be able to invoke analysis of the whole solution in one simple command:

```xml
<Project>

    <ItemGroup>
        <ProjectsToAnalyze Include="src\**\*.fsproj" />
    </ItemGroup>

    <Target Name="AnalyzeSolution">
        <MSBuild
                Projects="@(ProjectsToAnalyze)"
                Targets="AnalyzeProject" />
    </Target>

</Project>
```

At last, you can run the analyzer from the solution folder:

```shell
dotnet msbuild /t:AnalyzeSolution
```

## Project Cracking

If all this seems a bit complex to you, let us explain some inner details to give you a better understanding:  

The way the analyzers work is that we will programmatically type-check a project and process the results with our analyzers. In order to do this programmatically we need to construct the [FSharpProjectOptions](https://fsharp.github.io/fsharp-compiler-docs/reference/fsharp-compiler-codeanalysis-fsharpprojectoptions.html).  
This is essentially a type that represents all the fsharp compiler arguments. When using `--project`, we will use [ProjInfo](https://github.com/ionide/proj-info) to invoke a set of MSBuild targets in the project to perform a design-time build.  
A design-time build is basically an empty invocation of a build. It won't produce assemblies but will have constructed the correct arguments to theoretically invoke the compiler.  

There's an alternative way to do this. Instead of using the `--project` argument, it's possible to use the `--fsc-args` argument to let the CLI tool construct the needed `FSharpProjectOptions`.  
This also uses MSBuild, but in a more efficient way to provide us with the needed information.
Here's how the `Directory.Solution.targets` file would look like to make the use of `--fsc-args` possible:

```xml
<Project>

    <ItemGroup>
        <ProjectsToAnalyze Include="src\**\*.fsproj" />
    </ItemGroup>

    <Target Name="AnalyzeSolution">
        <Exec Command="dotnet build -c Release $(SolutionFileName)" />
        <MSBuild
                Projects="@(ProjectsToAnalyze)"
                Targets="AnalyzeProject"
                Properties="DesignTimeBuild=True;Configuration=Release;ProvideCommandLineArgs=True;SkipCompilerExecution=True" />
    </Target>

</Project>
```

And here's the `Directory.Build.targets`:
```xml
<Project>

    <Target
        Name="AnalyzeProject" 
        DependsOnTargets="Restore;ResolveAssemblyReferencesDesignTime;ResolveProjectReferencesDesignTime;ResolvePackageDependenciesDesignTime;FindReferenceAssembliesForReferences;_GenerateCompileDependencyCache;_ComputeNonExistentFileProperty;BeforeBuild;BeforeCompile;CoreCompile">
        
        <Message Importance="normal" Text="fsc arguments: @(FscCommandLineArgs)" />
        <Message Importance="High" Text="Analyzing $(MSBuildProjectFile)"/>
        <Exec
            ContinueOnError="true"
            Command="dotnet fsharp-analyzers --fsc-args &quot;@(FscCommandLineArgs)&quot; --analyzers-path &quot;$(PkgG-Research_FSharp_Analyzers)\analyzers\dotnet\fs&quot; --exclude-analyzer PartialAppAnalyzer --fail-on-warnings GRA-STRING-001 --verbose --report &quot;$(MSBuildProjectName)-analysis.sarif&quot;">
            <Output TaskParameter="ExitCode" PropertyName="LastExitCode" />
        </Exec>
        <Error Condition="'$(LastExitCode)' == '-2'" Text="Problems were found $(MSBuildProjectFile)" />

    </Target>

</Project>
*)

(**

[Next]({{fsdocs-next-page-link}})

*)
