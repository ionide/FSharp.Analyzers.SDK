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

## Code That Does Not Type Check

A type error is replaced with an error recovery node in the typed tree, so analyzers using the typed tree cannot see the code that failed to type check.
This happens, for example, when you analyze a project before its referenced projects are built, or a script that `#r`s an assembly that does not exist yet.
A `#load` or `#r` in a script that cannot be resolved leaves code out of the typed tree too, so it counts as a type error.
Warnings do not count, also when `TreatWarningsAsErrors` is set.
The type errors of files left out with `--exclude-files` or `--include-files` count too: those flags choose which files the analyzers run on, and a file that does not type check can still leave code out of the typed tree of the files that use it.

The tool always logs the type errors, and the `--report` SARIF file lists them under `toolExecutionNotifications` with `executionSuccessful` set to `false`.
By default the exit code does not change. Pass `--fail-on-type-errors` to exit with code `-5` when a project or script does not type check:

```shell
dotnet fsharp-analyzers --project ./YourProject.fsproj --fail-on-type-errors
```

A finding with severity `Error` and an analyzer that fails take precedence and keep their own exit codes.

## Viewing Additional Commands

You can view the full list of commands available by running:

```shell
dotnet fsharp-analyzers --help
```

[Next]({{fsdocs-next-page-link}})