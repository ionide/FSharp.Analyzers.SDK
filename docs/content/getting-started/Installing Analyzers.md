---
category: getting-started
categoryindex: 1
index: 1
---
# Installation

## Installing the Tool

A dotnet CLI tool, called [fsharp-analyzers](https://github.com/ionide/FSharp.Analyzers.SDK/), is used to run analyzers outside the context of an IDE. Add it to your tool-manifest with:

```shell
dotnet tool install fsharp-analyzers --create-manifest-if-needed
```

## Installing Analyzers

### Suggested Packages

1. [Ionide Analyzers](https://github.com/ionide/FSharp.Analyzers.SDK/)
2. [G-Research Analyzers](https://github.com/G-Research/fsharp-analyzers/)

### Nuget

If you are using Nuget as your package manager, add the `PackageReference` pointing to your favorite analyzers to the `.fsproj` file of the project you want to analyze.

```xml
<PackageReference Include="G-Research.FSharp.Analyzers" Version="0.12.1">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>analyzers</IncludeAssets>
</PackageReference>
<PackageReference Include="Ionide.Analyzers" Version="0.28.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>analyzers</IncludeAssets>
</PackageReference>
```

### Paket

If you are using Paket as your package manager, add the package to your `paket.dependencies` file. The example below uses a paket group, but it is not required.

```paket
group analyzers
    source https://api.nuget.org/v3/index.json

    nuget Ionide.Analyzers
    nuget G-Research.FSharp.Analyzers
```

[Next]({{fsdocs-next-page-link}})