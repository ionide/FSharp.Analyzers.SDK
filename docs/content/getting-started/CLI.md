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

## Overriding Severities

The `--treat-as-info`, `--treat-as-hint`, `--treat-as-warning` and `--treat-as-error` arguments change the severity of analyzer messages, regardless of the severity the analyzer reported.
This is useful in CI, where the tool only fails the build for messages with severity `Error`.

Each argument takes a list of analyzer codes. A code can end with a `*` wildcard to match every code starting with that prefix, and `*` on its own matches every code.

```shell
# Fail the build on every finding
dotnet fsharp-analyzers --project ./YourProject.fsproj --treat-as-error "*"

# Fail the build on every finding from one analyzer family
dotnet fsharp-analyzers --project ./YourProject.fsproj --treat-as-error "GRA-*"

# Fail on every GRA- finding, except GRA-STRING-001 which stays a warning
dotnet fsharp-analyzers --project ./YourProject.fsproj --treat-as-error "GRA-*" --treat-as-warning GRA-STRING-001
```

An exact code takes precedence over a pattern. Listing the same exact code in two arguments, or two overlapping patterns (such as `*` and `GRA-*`) in two arguments, is rejected.

## Viewing Additional Commands

You can view the full list of commands available by running:

```shell
dotnet fsharp-analyzers --help
```

[Next]({{fsdocs-next-page-link}})