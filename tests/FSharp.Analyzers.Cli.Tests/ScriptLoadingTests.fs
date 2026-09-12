module FSharp.Analyzers.Cli.Tests.ScriptLoadingTests

open System.IO
open NUnit.Framework
open Microsoft.Extensions.Logging.Abstractions
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Symbols
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Analyzers.Cli.ProjectLoading

/// Writes the source to a temporary script, type checks it the way the CLI does and
/// returns the project diagnostics together with the typed tree of the script.
let private checkScript (source: string) =
    let script = Path.Combine(Path.GetTempPath(), $"%s{Path.GetRandomFileName()}.fsx")

    File.WriteAllText(script, source)

    try
        let checker = Utils.createFCS None

        let options =
            loadScripts NullLogger.Instance checker [ script ]
            |> Async.RunSynchronously
            |> Array.exactlyOne

        let projectResults =
            checker.ParseAndCheckProject options
            |> Async.RunSynchronously

        let _, fileResults =
            checker.GetBackgroundCheckResultsForFileInProject(script, options)
            |> Async.RunSynchronously

        projectResults.Diagnostics, fileResults.ImplementationFile
    finally
        File.Delete script

/// Collects the full names of every call in the typed tree.
let private calls (typedTree: FSharpImplementationFileContents option) =
    let names = ResizeArray()

    let collector =
        { new TypedTreeCollectorBase() with
            override _.WalkCall _ mfv _ _ _ _ = names.Add mfv.FullName
        }

    typedTree
    |> Option.iter (walkTast collector)

    List.ofSeq names

[<Test>]
let ``a script type checks against the sdk reference assemblies`` () =
    let diagnostics, _ = checkScript "printfn \"%b\" (\"foo\".EndsWith(\"p\"))\n"

    let errors =
        diagnostics
        |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)
        |> Array.map (fun d -> d.Message)

    Assert.That(errors, Is.Empty)

// A construct that fails to type check is replaced with an error recovery node in the typed tree,
// which leaves nothing for a typed tree analyzer to match on.
// See https://github.com/ionide/FSharp.Analyzers.SDK/issues/332
[<Test>]
let ``the typed tree of a script contains bare top-level expressions`` () =
    let _, typedTree = checkScript "printfn \"%b\" (\"foo\".EndsWith(\"p\"))\n"

    Assert.That(calls typedTree, Does.Contain "System.String.EndsWith")

[<Test>]
let ``the typed tree of a script contains top-level do expressions`` () =
    let _, typedTree = checkScript "do printfn \"%b\" (\"foo\".EndsWith(\"p\"))\n"

    Assert.That(calls typedTree, Does.Contain "System.String.EndsWith")

[<Test>]
let ``the typed tree of a script contains the body of a top-level binding`` () =
    let _, typedTree = checkScript "let f (y: int) = string y\n"

    Assert.That(calls typedTree, Does.Contain "Microsoft.FSharp.Core.Operators.string")
