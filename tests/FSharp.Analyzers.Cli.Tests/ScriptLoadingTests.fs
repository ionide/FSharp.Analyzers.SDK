module FSharp.Analyzers.Cli.Tests.ScriptLoadingTests

open System
open System.IO
open NUnit.Framework
open Microsoft.Extensions.Logging.Abstractions
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Symbols
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.CodeAnalysis
open FSharp.Analyzers.Cli.ProjectLoading
open Ionide.ProjInfo

let private toolsPath =
    lazy (Init.init (DirectoryInfo Environment.CurrentDirectory) None)

/// Writes the source to a temporary script and loads it the way the CLI does.
let private withScript (source: string) (f: string -> 'a) =
    let script = Path.Combine(Path.GetTempPath(), $"%s{Path.GetRandomFileName()}.fsx")

    File.WriteAllText(script, source)

    try
        f script
    finally
        File.Delete script

let private loadScript checker script =
    loadScripts NullLogger.Instance toolsPath.Value checker [ script ]
    |> Async.RunSynchronously
    |> Array.exactlyOne

let private errors (diagnostics: FSharpDiagnostic array) =
    diagnostics
    |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)
    |> Array.map (fun d -> d.Message)

/// Type checks the script with the given options and returns the project diagnostics.
let private projectDiagnostics (checker: FSharpChecker) (options: FSharpProjectOptions) =
    let projectResults =
        checker.ParseAndCheckProject options
        |> Async.RunSynchronously

    projectResults.Diagnostics

/// Writes the source to a temporary script, type checks it the way the CLI does and
/// returns the project diagnostics together with the typed tree of the script.
let private checkScript (source: string) =
    withScript
        source
        (fun script ->
            let checker = Utils.createFCS None
            let options, _ = loadScript checker script

            let _, fileResults =
                checker.GetBackgroundCheckResultsForFileInProject(script, options)
                |> Async.RunSynchronously

            projectDiagnostics checker options, fileResults.ImplementationFile
        )

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

    Assert.That(errors diagnostics, Is.Empty)

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

// `dotnet fsi` references FSharp.Compiler.Interactive.Settings.dll implicitly, so a script may use `fsi`
// without saying anything. FCS does not find that assembly from a dotnet tool on its own.
// See https://github.com/ionide/FSharp.Analyzers.SDK/issues/334
[<Test>]
let ``a script can use the fsi object`` () =
    let diagnostics, _ = checkScript "let args: string array = fsi.CommandLineArgs\n"

    Assert.That(errors diagnostics, Is.Empty)

[<Test>]
let ``the typed tree of a script contains a call on the fsi object`` () =
    let _, typedTree =
        checkScript "printfn \"%b\" (fsi.CommandLineArgs[0].EndsWith(\"p\"))\n"

    Assert.That(calls typedTree, Does.Contain "System.String.EndsWith")

// A #load that cannot be resolved leaves the code it would have brought in out of the typed tree,
// without a type error in the script itself.
// See https://github.com/ionide/FSharp.Analyzers.SDK/issues/350
[<Test>]
let ``a #load that cannot be resolved is a load error`` () =
    let loadErrors =
        withScript
            "#load \"does-not-exist.fsx\"\nlet x = 1\n"
            (fun script ->
                let checker = Utils.createFCS None
                snd (loadScript checker script)
            )

    Assert.That(loadErrors, Is.Not.Empty)

[<Test>]
let ``a script without load errors has none`` () =
    let loadErrors =
        withScript
            "let x = 1\n"
            (fun script ->
                let checker = Utils.createFCS None
                snd (loadScript checker script)
            )

    Assert.That(loadErrors, Is.Empty)

// TreatWarningsAsErrors passes --warnaserror+, which gives a warning error severity.
// The typed tree is complete, so it must not count as a type check failure.
[<Test>]
let ``a warning is not a type error under warnaserror`` () =
    let warnAsErrorDiagnostics, diagnostics =
        withScript
            "let f x =\n    match x with\n    | 1 -> ()\n"
            (fun script ->
                let checker = Utils.createFCS None
                let options, _ = loadScript checker script

                let options =
                    { options with
                        OtherOptions = Array.append options.OtherOptions [| "--warnaserror+" |]
                    }

                let withoutWarnAsError =
                    { options with
                        OtherOptions = Array.filter (isWarnAsError >> not) options.OtherOptions
                    }

                projectDiagnostics checker options, projectDiagnostics checker withoutWarnAsError
            )

    // Without --warnaserror+ the incomplete match above would not be an error, so the test would prove nothing.
    Assert.That(errors warnAsErrorDiagnostics, Is.Not.Empty)
    Assert.That(errors diagnostics, Is.Empty)

// An unresolved #r leaves out the code that would use the assembly, so runProject must see it.
// FCS reports it in the check results of the file, not in the ones of the project.
// See https://github.com/ionide/FSharp.Analyzers.SDK/issues/350
[<Test>]
let ``an assembly reference that cannot be resolved is only reported for the file`` () =
    let loadErrors, projectErrors, fileErrors =
        withScript
            "#r \"missing/Library.dll\"\nlet x = 1\n"
            (fun script ->
                let checker = Utils.createFCS None
                let options, loadErrors = loadScript checker script

                let _, fileResults =
                    checker.GetBackgroundCheckResultsForFileInProject(script, options)
                    |> Async.RunSynchronously

                loadErrors,
                errors (projectDiagnostics checker options),
                errors fileResults.Diagnostics
            )

    Assert.That(loadErrors, Is.Empty)
    Assert.That(projectErrors, Is.Empty)
    Assert.That(fileErrors, Has.Some.Contains "missing/Library.dll")
