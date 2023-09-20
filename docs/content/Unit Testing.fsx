(**
---
category: end-users
categoryindex: 1
index: 4
---

# Unit testing an analyzer

To help analyzer authors testing their analyzers there's dedicated [testing package](https://www.nuget.org/packages/FSharp.Analyzers.SDK.Testing).
It contains easy to use utility functions to create a context for the analyzer and assertion helpers.  

`FSharp.Analyzers.SDK.Testing.mkOptionsFromProject` creates the `FSharpProjectOptions` for the given framework (e.g. `net7.0`) and the given list of packages to reference.  
`FSharp.Analyzers.SDK.Testing.getContext` then takes the `FSharpProjectOptions` and the source code to test and creates a `CliContext` you can pass along to your analyzer function.  
The module `FSharp.Analyzers.SDK.Testing.Assert` offers various functions to help with assertion statements from your favorite unit testing framework.  
For a complete example of an unit testing project, take a look at `OptionAnalyzer.Test` in the `samples` folder of the SDK repository.
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
                    {
                        Name = "Newtonsoft.Json"
                        Version = "13.0.3"
                    }
                    {
                        Name = "Fantomas.FCS"
                        Version = "6.2.0"
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
