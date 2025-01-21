---
category: getting-started
categoryindex: 1
index: 3
---

# Command Line Arguments

## Example Command

When running the CLI tool from the command line, the bare minimum you need to provide is the path to the project file(s) you want to analyze.

```shell
dotnet fsharp-analyzers --project ./YourProject.fsproj
```

An optional argument you may need to provide is `--analyzers-path`. This is the path to the directory containing the analyzer DLLs. 

```shell
dotnet fsharp-analyzers --project ./YourProject.fsproj --analyzers-path ./path/to/analyzers/directory
```

⚠️ If you don't provide this argument, it will default to `packages/analyzers`.

## Viewing Additional Commands

You can view the full list of commands available by running:

```shell
dotnet fsharp-analyzers --help
```

[Next]({{fsdocs-next-page-link}})