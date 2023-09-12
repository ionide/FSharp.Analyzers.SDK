(**
---
category: end-users
categoryindex: 1
index: 1
---

# Getting started

## Create project

Create a new class library targeting `net6.0`

```shell
dotnet new classlib -lang F# -f net6.0 -n OptionValueAnalyzer
```

Note that the assembly name needs to contain `Analyzer` in the name in order for it to be picked up.

Add a reference to the analyzers SDK:

```shell
dotnet add package FSharp.Analyzers.SDK
```

```shell
paket add FSharp.Analyzers.SDK
```

## First analyzer

An [Analyzer](../reference/fsharp-analyzers-sdk-analyzer.html) is a function that takes a `Context` and returns a list of `Message`.
*)

(*** hide ***)
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Analyzers.SDK.dll"
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Compiler.Service.dll"
(** *)

module OptionAnalyzer =

    open FSharp.Analyzers.SDK

    // This attribute is required!
    [<Analyzer>]
    let optionValueAnalyzer: Analyzer =
        fun (context: Context) ->
            // inspect context to determine the error/warning messages
            // A potential implementation might traverse the untyped syntax tree
            // to find any references of `Option.Value`
            [
                {
                    Type = "Option.Value analyzer"
                    Message = "Option.Value shouldn't be used"
                    Code = "OV001"
                    Severity = Warning
                    Range = FSharp.Compiler.Text.Range.Zero
                    Fixes = []
                }
            ]

(**
## Running your first analyzer

After building your project you can run your analyzer on a project of your choosing using the [fsharp-analyzers](https://www.nuget.org/packages/fsharp-analyzers) tool.

```shell
dotnet tool install --global fsharp-analyzers
```

```shell
fsharp-analyzers --project YourProject.fsproj --analyzers-path ./OptionAnalyzer/bin/Release --verbose
```
*)
