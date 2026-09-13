module FSharp.Analyzers.Cli.Tests.ArgumentsTests

open Argu
open NUnit.Framework
open FSharp.Analyzers.Cli

let private parser = ArgumentParser.Create<Arguments>()

let private parse argv =
    parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)

[<Test>]
let ``a single list flag keeps all its values`` () =
    let results =
        parse
            [|
                "--script"
                "a.fsx"
                "b.fsx"
            |]

    Assert.That(
        Arguments.getAll results <@ Script @>,
        Is.EqualTo
            [
                "a.fsx"
                "b.fsx"
            ]
    )

[<Test>]
let ``a repeated list flag accumulates instead of keeping the last occurrence`` () =
    let results =
        parse
            [|
                "--script"
                "a.fsx"
                "--script"
                "b.fsx"
            |]

    Assert.That(
        Arguments.getAll results <@ Script @>,
        Is.EqualTo
            [
                "a.fsx"
                "b.fsx"
            ]
    )

[<Test>]
let ``an absent list flag yields an empty list`` () =
    let results =
        parse
            [|
                "--project"
                "a.fsproj"
            |]

    Assert.That(Arguments.getAll results <@ Script @>, Is.Empty)

/// Every list-valued flag is expected to behave the same, see
/// https://github.com/ionide/FSharp.Analyzers.SDK/issues/336
[<TestCase("--project")>]
[<TestCase("--script")>]
[<TestCase("--analyzers-path")>]
[<TestCase("--treat-as-info")>]
[<TestCase("--treat-as-hint")>]
[<TestCase("--treat-as-warning")>]
[<TestCase("--treat-as-error")>]
[<TestCase("--exclude-files")>]
[<TestCase("--include-files")>]
[<TestCase("--exclude-analyzers")>]
[<TestCase("--include-analyzers")>]
let ``every list flag can be repeated`` (flag: string) =
    let results =
        parse
            [|
                flag
                "one"
                flag
                "two"
            |]

    Assert.That(results.GetAllResults(), Has.Exactly(2).Items)
