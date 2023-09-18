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

An [Analyzer<'TContext>](../reference/fsharp-analyzers-sdk-analyzer-1.html) is a function that takes a `Context` and returns a list of `Message`.  
There are two flavours of analyzers:

- Console application analyzers ([CliAnalyzer](../reference/fsharp-analyzers-sdk-clianalyzerattribute.html))
- Editor analyzers ([EditorAnalyzer](../reference/fsharp-analyzers-sdk-editoranalyzerattribute.html))

The key difference between them is that the console application analyzer will have the *full project* information.  
Per file this includes the untyped tree, typed tree, type-check results of the file and project type-check results.  
The [fsharp-analyzers](https://www.nuget.org/packages/fsharp-analyzers) tool will collect all this information upfront and pass it down to the analyzer via the [CliContext](../reference/fsharp-analyzers-sdk-clicontext.html).

In the case of an editor analyzer, the IDE might not have all the available information available and will be more selective in what it can pass down to the analyzer.
The main reasoning behind this is performance. It might be desirable for some analyzers to run after every keystroke, while others should be executed more sparingly.

In the following example we will be 
*)

(*** hide ***)
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Analyzers.SDK.dll"
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Compiler.Service.dll"
(** *)

module OptionAnalyzer =

    open FSharp.Analyzers.SDK

    // This attribute is required and needs to match the correct context type!
    [<CliAnalyzer>]
    let optionValueAnalyzer: Analyzer<CliContext> =
        fun (context: CliContext) ->
            async {
                // inspect context to determine the error/warning messages
                // A potential implementation might traverse the untyped syntax tree
                // to find any references of `Option.Value`
                return
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
            }

(**
## Running your first analyzer

After building your project you can run your analyzer on a project of your choosing using the [fsharp-analyzers](https://www.nuget.org/packages/fsharp-analyzers) tool.  
Again, please verify your analyzer is a `CliAnalyzerAttribute` and uses the `CliContext`!

```shell
dotnet tool install --global fsharp-analyzers
```

```shell
fsharp-analyzers --project YourProject.fsproj --analyzers-path ./OptionAnalyzer/bin/Release --verbose
```

[Next]({{fsdocs-next-page-link}})

*)
