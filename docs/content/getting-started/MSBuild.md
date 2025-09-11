---
category: getting-started
categoryindex: 1
index: 4
---

# MSBuild

## Analyzer paths

Finding the analyzer DLLs can be tricky across different environments. Luckily, MSBuild computes these paths after restore.
We can add a solution-level target that invokes the analyzer CLI with the correct paths.

## Solution targets

MSBuild targets defined in `Directory.Solution.targets` can be invoked with `dotnet msbuild /t:YourTargetName`. We can create a target that reads the generated NuGet `*.nuget.g.props` file to discover analyzer package paths and collect all the projects we wish to analyze:

_Directory.Solution.targets_

```xml
<Project>
	<!-- Import the NuGet props file to get access to Pkg* variables -->
	<Import Project="artifacts/obj/Telplin/Telplin.fsproj.nuget.g.props" Condition="Exists('artifacts/obj/Telplin/Telplin.fsproj.nuget.g.props')"/>
	<ItemGroup>
		<ProjectsToAnalyze Include="src/**/*.fsproj"/>
	</ItemGroup>
	<Target Name="AnalyzeSolution" Condition="Exists('artifacts/obj/Telplin/Telplin.fsproj.nuget.g.props')">
		<PropertyGroup>
			<CodeRoot>$(SolutionDir)</CodeRoot>
		</PropertyGroup>
		<PropertyGroup>
			<FSharpAnalyzersOtherFlags>--analyzers-path &quot;$(PkgG-Research_FSharp_Analyzers)/analyzers/dotnet/fs&quot;</FSharpAnalyzersOtherFlags>
			<FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --analyzers-path &quot;$(PkgIonide_Analyzers)/analyzers/dotnet/fs&quot;</FSharpAnalyzersOtherFlags>
			<FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --configuration $(Configuration)</FSharpAnalyzersOtherFlags>
			<FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --verbosity d</FSharpAnalyzersOtherFlags>
			<FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --code-root $(CodeRoot)</FSharpAnalyzersOtherFlags>
			<FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --report &quot;$(CodeRoot)/analysis.sarif&quot;</FSharpAnalyzersOtherFlags>
		</PropertyGroup>
		<Delete Files="$(SolutionDir)/analysis.sarif" Condition="Exists('$(SolutionDir)/analysis.sarif')"/>
		<!-- Execute fsharp-analyzers with all projects in a single process -->
		<Exec Command="dotnet fsharp-analyzers $(FSharpAnalyzersOtherFlags) @(ProjectsToAnalyze->'--project &quot;%(FullPath)&quot;', ' ')"
              ContinueOnError="true"/>
	</Target>
</Project>
```

Run it with

```shell
dotnet msbuild /t:AnalyzeSolution
```

Notes:

- `artifacts/obj/Telplin/Telplin.fsproj.nuget.g.props` is generated after `dotnet restore` runs.
  The target depends on it, hence the `Condition="Exists('...nuget.g.props')"` check.
- `--code-root` is important for certain tooling to have the correct links from the problems found to your source code.
- `$(PkgG-Research_FSharp_Analyzers)` becomes available when your solution references the [G-Research.FSharp.Analyzers](https://www.nuget.org/packages/G-Research.FSharp.Analyzers) package. Expect a similar `$(Pkg...)` property for whichever analyzer package your project uses.

[Next]({{fsdocs-next-page-link}})
