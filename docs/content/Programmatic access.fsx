(**
---
category: end-users
categoryindex: 1
index: 3
---

# Programmatically running an analyzer

Using the [SDK](https://www.nuget.org/packages/FSharp.Analyzers.SDK), it is possible to invoke analyzers programmatically.  
This can be done via the [Client](../reference/fsharp-analyzers-sdk-client-2.html) type.  
The `Client` needs to know what type of analyzer you intend to load: *console* or *editor*.
*)

(*** hide ***)
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Analyzers.SDK.dll"
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Compiler.Service.dll"
(** *)

open FSharp.Analyzers.SDK

let client = Client<CliAnalyzerAttribute, CliContext>()
let countLoaded = client.LoadAnalyzers ignore @"C:\MyAnalyzers"
let ctx = Unchecked.defaultof<CliContext> // Construct your context...
client.RunAnalyzers(ctx)

(**
[Previous]({{fsdocs-previous-page-link}})
*)
