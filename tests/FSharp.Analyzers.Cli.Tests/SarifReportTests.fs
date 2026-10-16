module FSharp.Analyzers.Cli.Tests.SarifReportTests

open System
open System.IO
open System.Text.Json
open NUnit.Framework
open Microsoft.Extensions.Logging.Abstractions
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Text
open FSharp.Analyzers.Cli

let private codeRoot = Path.GetTempPath()

/// Writes a report for the failures and returns the invocation of its run.
let private invocationWith
    (typeCheckFailures: TypeCheckFailure list)
    (analyzerFailures: AnalyzerFailure list)
    =
    let report = Path.Combine(codeRoot, $"%s{Path.GetRandomFileName()}.sarif")

    try
        SarifReport.writeReport
            NullLogger.Instance
            []
            typeCheckFailures
            analyzerFailures
            (Output.codeRootUri codeRoot)
            report

        use json = JsonDocument.Parse(File.ReadAllText report)

        json.RootElement.GetProperty("runs").[0].GetProperty("invocations").[0].Clone()
    finally
        File.Delete report

let private invocation (typeCheckFailures: TypeCheckFailure list) =
    invocationWith typeCheckFailures []

let private typeError (range: range) =
    FSharpDiagnostic.Create(
        FSharpDiagnosticSeverity.Error,
        "The namespace or module 'Library' is not defined.",
        39,
        range
    )

let private notifications (invocation: JsonElement) =
    match invocation.TryGetProperty "toolExecutionNotifications" with
    | true, notifications -> List.ofSeq (notifications.EnumerateArray())
    | false, _ -> []

[<Test>]
let ``a run where everything type checks is successful`` () =
    let invocation = invocation []

    Assert.That(invocation.GetProperty("executionSuccessful").GetBoolean(), Is.True)
    Assert.That(notifications invocation, Is.Empty)

// See https://github.com/ionide/FSharp.Analyzers.SDK/issues/350
[<Test>]
let ``a type error makes the run unsuccessful and is reported as a notification`` () =
    let script = Path.Combine(codeRoot, "a.fsx")

    let invocation =
        invocation
            [
                {
                    ProjectFileName = Path.Combine(codeRoot, "a.fsx.fsproj")
                    Errors =
                        [
                            typeError (
                                Range.mkRange script (Position.mkPos 3 6) (Position.mkPos 3 13)
                            )
                        ]
                }
            ]

    Assert.That(invocation.GetProperty("executionSuccessful").GetBoolean(), Is.False)

    let notification = List.exactlyOne (notifications invocation)

    Assert.That(notification.GetProperty("level").GetString(), Is.EqualTo "error")

    Assert.That(fst (notification.TryGetProperty "descriptor"), Is.False)

    Assert.That(
        notification.GetProperty("message").GetProperty("text").GetString(),
        Does.Contain "FS0039: The namespace or module 'Library' is not defined."
    )

    let physicalLocation =
        notification.GetProperty("locations").[0].GetProperty("physicalLocation")

    Assert.That(
        physicalLocation.GetProperty("artifactLocation").GetProperty("uri").GetString(),
        Is.EqualTo "a.fsx"
    )

    Assert.That(
        physicalLocation.GetProperty("region").GetProperty("startLine").GetInt32(),
        Is.EqualTo 3
    )

    Assert.That(
        physicalLocation.GetProperty("region").GetProperty("startColumn").GetInt32(),
        Is.EqualTo 7
    )

[<Test>]
let ``a type error without a source location is reported without a location`` () =
    let invocation =
        invocation
            [
                {
                    ProjectFileName = Path.Combine(codeRoot, "Project.fsproj")
                    Errors = [ typeError Range.range0 ]
                }
            ]

    Assert.That(invocation.GetProperty("executionSuccessful").GetBoolean(), Is.False)

    let notification = List.exactlyOne (notifications invocation)

    Assert.That(fst (notification.TryGetProperty "locations"), Is.False)

// An analyzer that throws loses its messages for the file, so the run only covered part of the code.
[<Test>]
let ``an analyzer that throws makes the run unsuccessful and is reported as a notification`` () =
    let invocation =
        invocationWith
            []
            [
                {
                    AnalyzerName = "ThrowingAnalyzer"
                    FileName = Path.Combine(codeRoot, "A.fs")
                    Exception = InvalidOperationException "boom"
                }
            ]

    Assert.That(invocation.GetProperty("executionSuccessful").GetBoolean(), Is.False)

    let notification = List.exactlyOne (notifications invocation)

    Assert.That(notification.GetProperty("level").GetString(), Is.EqualTo "error")

    Assert.That(
        notification.GetProperty("message").GetProperty("text").GetString(),
        Does.Contain "Analyzer ThrowingAnalyzer failed"
    )

    Assert.That(
        notification.GetProperty("exception").GetProperty("message").GetString(),
        Is.EqualTo "boom"
    )

    Assert.That(
        notification
            .GetProperty("locations")
            .[0].GetProperty("physicalLocation")
            .GetProperty("artifactLocation")
            .GetProperty("uri")
            .GetString(),
        Is.EqualTo "A.fs"
    )
