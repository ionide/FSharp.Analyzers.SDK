(**
---
category: end-users
categoryindex: 1
index: 4
---

# Unit testing an analyzer

To help analyzer authors testing their analyzers, there's a dedicated [testing package](https://www.nuget.org/packages/FSharp.Analyzers.SDK.Testing).
It contains easy to use utility functions to create a context for the analyzer and assertion helpers.  

[`FSharp.Analyzers.SDK.Testing.mkOptionsFromProject`](../reference/fsharp-analyzers-sdk-testing.html#mkOptionsFromProject) creates the [`FSharpProjectOptions`](https://fsharp.github.io/fsharp-compiler-docs/reference/fsharp-compiler-codeanalysis-fsharpprojectoptions.html) for the given framework (e.g. `net7.0`) and the given list of packages to reference.  
[`FSharp.Analyzers.SDK.Testing.getContext`](../reference/fsharp-analyzers-sdk-testing.html#getContext) then takes the `FSharpProjectOptions` and the source code to test and creates a [`CliContext`](../reference/fsharp-analyzers-sdk-clicontext.html) you can pass along to your analyzer function.  
The module [`FSharp.Analyzers.SDK.Testing.Assert`](../reference/fsharp-analyzers-sdk-testing-assert.html) offers various functions to help with assertion statements from your favorite unit testing framework.  
For a complete example of an unit testing project, take a look at [`OptionAnalyzer.Test`](https://github.com/ionide/FSharp.Analyzers.SDK/tree/7b7ec530c507d765ab18d93ebb7aa45ab59accc2/samples/OptionAnalyzer.Test) in the `samples` folder of the SDK repository.
*)

(*** hide ***)
#r "../../src/FSharp.Analyzers.SDK.Testing/bin/Release/net6.0/FSharp.Analyzers.SDK.dll"
#r "../../src/FSharp.Analyzers.SDK.Testing/bin/Release/net6.0/FSharp.Analyzers.SDK.Testing.dll"
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Compiler.Service.dll"
#r "../../samples/OptionAnalyzer.Test/bin/Release/net6.0/nunit.framework.dll"
#r "../../samples/OptionAnalyzer.Test/bin/Release/net6.0/OptionAnalyzer.dll"
(** *)

open FSharp.Compiler.CodeAnalysis
open FSharp.Analyzers.SDK.Testing
open OptionAnalyzer
open NUnit.Framework

let mutable projectOptions: FSharpProjectOptions = FSharpProjectOptions.zero

[<SetUp>]
let Setup () =
    task {
        let! opts =
            mkOptionsFromProject
                "net7.0"
                [
                    // The SDK uses this in a "dotnet add package x --version y" command
                    // to generate the needed FSharpProjectOptions
                    {
                        Name = "Newtonsoft.Json"
                        Version = "13.0.3"
                    }
                ]

        projectOptions <- opts
    }

[<Test>]
let ``warnings are emitted`` () =
    async {
        let source =
            """
module M

let notUsed() =
    let option : Option<int> = None
    option.Value
    """

        let ctx = getContext projectOptions source
        let! msgs = optionValueAnalyzer ctx
        Assert.IsNotEmpty msgs
        Assert.IsTrue(Assert.messageContains "Option.Value" msgs[0])
    }

(**
[Previous]({{fsdocs-previous-page-link}})
*)
