(**
---
category: end-users
categoryindex: 1
index: 1
---

# Getting started using analyzers

## Premise

We assume the analyzers to be used are distributed as a nuget package.

## Using analyzers in a single project

First, we need to add the `fsharp-analyzers` dotnet tool to the tool-manifest.
```shell
dotnet tool install fsharp-analyzers
```

Next, add the `PackageReference` pointing to your favorite analyzers to the `.fsproj` file of the project you want to analyzse:

```xml
<PackageReference Include="G-Research.FSharp.Analyzers" Version="0.1.6">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>build</IncludeAssets>
</PackageReference>
```

Finally, add a custom MSBuild target for easy invocation of the analyzer:

```xml
<Target
    Name="AnalyzeProject" 
    DependsOnTargets="Restore;ResolveAssemblyReferencesDesignTime;ResolveProjectReferencesDesignTime;ResolvePackageDependenciesDesignTime;FindReferenceAssembliesForReferences;_GenerateCompileDependencyCache;_ComputeNonExistentFileProperty;BeforeBuild;BeforeCompile;CoreCompile">

    <Message Importance="High" Text="Analyzing $(MSBuildProjectFile)"/>
    <Exec
        ContinueOnError="true"
        Command="dotnet fsharp-analyzers --project &quot;$(MSBuildProjectFile)&quot; --analyzers-path &quot;$(PkgG-Research_FSharp_Analyzers)\analyzers\dotnet\fs&quot; --exclude-analyzer PartialAppAnalyzer --fail-on-warnings GRA-STRING-001 GRA-STRING-002 GRA-STRING-003 GRA-UNIONCASE-001 --verbose --report &quot;$(MSBuildProjectName)-analysis.sarif&quot;">
        <Output TaskParameter="ExitCode" PropertyName="LastExitCode" />
    </Exec>
    <Error Condition="'$(LastExitCode)' == '-2'" Text="Problems were found $(MSBuildProjectFile)" />
</Target>
```

You may need to adjust the `Command` to be compatible with your specific analyzer. Think about how you want warnings to be treated.

At last, we can run the analyzer from the project folder:

```shell
dotnet msbuild /t:AnalyzeProject
```
*)
(**

[Next]({{fsdocs-next-page-link}})

*)
