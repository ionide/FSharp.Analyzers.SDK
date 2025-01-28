---
category: getting-started
categoryindex: 1
index: 3
---

# Command Line Arguments

## Example Command

When running the CLI tool from the command line (and after installing analyzers), the minimum console arguments you need to provide is the path to the project file(s) you want to analyze.

```shell
dotnet fsharp-analyzers --project ./YourProject.fsproj --analyzers-path ./path/to/analyzers/directory
```

⚠️ If you don't provide the `--analyzers-path` argument, it will default to `packages/analyzers`. If you are using Paket with a group called `analyzers`, this default path should work for you.

## Viewing Additional Commands

You can view the full list of commands available by running:

```shell
dotnet fsharp-analyzers --help
```

[Next]({{fsdocs-next-page-link}})