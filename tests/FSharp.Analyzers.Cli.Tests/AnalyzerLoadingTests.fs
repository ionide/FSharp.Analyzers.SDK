module FSharp.Analyzers.Cli.Tests.AnalyzerLoadingTests

open System
open System.IO
open NUnit.Framework
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Analyzers.SDK

/// Compiles the source to a library at outputPath, referencing the assemblies this test runs with.
let private compile (references: string list) (outputPath: string) (source: string) =
    let sourcePath = Path.ChangeExtension(outputPath, ".fs")
    File.WriteAllText(sourcePath, source)

    let platformAssemblies =
        (AppContext.GetData "TRUSTED_PLATFORM_ASSEMBLIES" :?> string).Split Path.PathSeparator
        |> List.ofArray

    let args =
        [
            "fsc.exe"
            $"-o:%s{outputPath}"
            "--target:library"
            "--targetprofile:netcore"
            "--noframework"
            "--nowarn:3370"
            yield!
                List.map
                    (sprintf "-r:%s")
                    (platformAssemblies
                     @ references)
            sourcePath
        ]

    let diagnostics, error =
        FSharpChecker.Create().Compile(Array.ofList args)
        |> Async.RunSynchronously

    let errors =
        diagnostics
        |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)

    if
        not (Array.isEmpty errors)
        || Option.isSome error
    then
        failwith $"Compiling %s{outputPath} failed: %A{errors} %A{error}"

[<Test>]
let ``an analyzer assembly with a dependency that cannot be loaded is skipped`` () =
    let root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
    let dependencyDir = Path.Combine(root, "dependency")
    let analyzersDir = Path.Combine(root, "analyzers")

    Directory.CreateDirectory dependencyDir
    |> ignore

    Directory.CreateDirectory analyzersDir
    |> ignore

    try
        // Missing.dll stays out of the analyzers directory, so it cannot be resolved at load time.
        let missingDll = Path.Combine(dependencyDir, "Missing.dll")
        compile [] missingDll "namespace Missing\n\ntype Thing() =\n    member _.X = 1\n"

        compile
            [ missingDll ]
            (Path.Combine(analyzersDir, "BrokenAnalyzer.dll"))
            """
module BrokenAnalyzer

open FSharp.Analyzers.SDK

type Derived() =
    inherit Missing.Thing()

[<CliAnalyzer "BrokenAnalyzer">]
let broken: Analyzer<CliContext> = fun _ -> async { return [] }
"""

        compile
            []
            (Path.Combine(analyzersDir, "GoodAnalyzer.dll"))
            """
module GoodAnalyzer

open FSharp.Analyzers.SDK

[<CliAnalyzer "GoodAnalyzer">]
let good: Analyzer<CliContext> = fun _ -> async { return [] }
"""

        let stats = Client<CliAnalyzerAttribute, CliContext>().LoadAnalyzers analyzersDir

        Assert.That(stats.AnalyzerNames, Is.EqualTo [ "GoodAnalyzer" ])
        Assert.That(stats.AnalyzerAssemblies, Is.EqualTo 1)
        Assert.That(stats.FailedAssemblies, Is.EqualTo 1)
    finally
        try
            Directory.Delete(root, true)
        with _ ->
            ()
