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
[<TestCase("--define")>]
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

[<Test>]
let ``a define after the scripts is not taken as a script`` () =
    let results =
        parse
            [|
                "--script"
                "a.fsx"
                "--define"
                "FOO"
                "BAR"
            |]

    Assert.That(Arguments.getAll results <@ Script @>, Is.EqualTo [ "a.fsx" ])

    Assert.That(
        Arguments.getAll results <@ Define @>,
        Is.EqualTo
            [
                "FOO"
                "BAR"
            ]
    )

[<Test>]
let ``the script limit is 4 by default`` () =
    let results =
        parse
            [|
                "--script"
                "a.fsx"
            |]

    Assert.That(Arguments.maxConcurrentScripts results, Is.EqualTo(Ok 4: Result<int, int>))

[<Test>]
let ``the number of scripts checked at the same time can be set`` () =
    let results =
        parse
            [|
                "--max-concurrent-scripts"
                "1"
            |]

    Assert.That(Arguments.maxConcurrentScripts results, Is.EqualTo(Ok 1: Result<int, int>))

[<TestCase("0")>]
[<TestCase("-1")>]
let ``fewer than one script at a time is rejected`` (value: string) =
    let results =
        parse
            [|
                "--max-concurrent-scripts"
                value
            |]

    Assert.That(
        Arguments.maxConcurrentScripts results,
        Is.EqualTo(Error(int value): Result<int, int>)
    )

[<Test>]
let ``conditional compilation symbols are accepted as defines`` () =
    let results =
        parse
            [|
                "--define"
                "FOO"
                "_Bar1"
                "Baz'"
            |]

    Assert.That(
        Arguments.defines results,
        Is.EqualTo(
            Ok
                [
                    "FOO"
                    "_Bar1"
                    "Baz'"
                ]
            : Result<string list, string list>
        )
    )

[<TestCase("A;B")>]
[<TestCase("A B")>]
[<TestCase("1A")>]
[<TestCase("")>]
let ``a define that is not one symbol is rejected`` (value: string) =
    let results =
        parse
            [|
                "--define"
                "FOO"
                value
            |]

    Assert.That(
        Arguments.defines results,
        Is.EqualTo(Error [ value ]: Result<string list, string list>)
    )
