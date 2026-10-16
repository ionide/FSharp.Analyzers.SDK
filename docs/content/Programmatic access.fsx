(**
---
category: end-users
categoryindex: 2
index: 3
---

# Programmatically running an analyzer

Using the [SDK](https://www.nuget.org/packages/FSharp.Analyzers.SDK), it is possible to invoke analyzers programmatically.  
This can be done via the [Client](../reference/fsharp-analyzers-sdk-client-2.html) type.  
The `Client` needs to know what type of analyzer you intend to load: *console* or *editor*.
*)

(*** hide ***)
#r "../../artifacts/bin/FSharp.Analyzers.SDK/release/FSharp.Analyzers.SDK.dll"
#r "../../artifacts/bin/FSharp.Analyzers.Cli/release/FSharp.Compiler.Service.dll"
#r "../../artifacts/bin/FSharp.Analyzers.Cli/release/Microsoft.Extensions.Logging.Abstractions.dll"
(** *)

open FSharp.Analyzers.SDK

let client = Client<CliAnalyzerAttribute, CliContext>()
let countLoaded = client.LoadAnalyzers @"C:\MyAnalyzers"
let ctx = Unchecked.defaultof<CliContext> // Construct your context...

(**
`RunAnalyzers` does not throw. It returns a result per analyzer, with either the messages of that analyzer or the exception it threw.
An analyzer that throws does not affect the messages of the other analyzers.
*)

async {
    let! results = client.RunAnalyzers(ctx)

    for result in results do
        match result.Output with
        | Ok messages ->
            for message in messages do
                printfn "%s: %s" message.Message.Code message.Message.Message
        | Error error -> printfn "Analyzer %s failed: %s" result.AnalyzerName error.Message
}

(**
[Previous]({{fsdocs-previous-page-link}})
[Next]({{fsdocs-next-page-link}})
*)
