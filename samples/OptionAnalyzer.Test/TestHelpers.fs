module OptionAnalyzer.TestHelpers

open NUnit.Framework
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.Testing

let runtimeTfm =
    let v = System.Environment.Version

    "net"
    + string v.Major
    + "."
    + string v.Minor

let testPackages: Package list =
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

let mkTestProjectOptions () =
    mkOptionsFromProject runtimeTfm testPackages

let mkTestProjectSnapshot () =
    mkSnapshotFromProject runtimeTfm testPackages

/// Source snippets shared by RunAnalyzersSafely and RunAnalyzers tests.
module ClientTestSources =

    let optionValue =
        """
module M

let notUsed() =
    let option : Option<int> = None
    option.Value
    """

    let ignoreNextLine =
        """
module M

let notUsed() =
    let option : Option<int> = None
    // fsharpanalyzer: ignore-line-next OV001
    option.Value
    """

    let ignoreCurrentLine =
        """
module M

let notUsed() =
    let option : Option<int> = None
    option.Value // fsharpanalyzer: ignore-line OV001
    """

    let ignoreFile =
        """
// fsharpanalyzer: ignore-file OV001
module M

let notUsed() =
    let option : Option<int> = None
    option.Value
    """

    let ignoreRange =
        """
// fsharpanalyzer: ignore-region-start OV001
module M

let notUsed() =
    let option : Option<int> = None
    option.Value
// fsharpanalyzer: ignore-region-end
    """

/// Create a Client, load analyzers from the current directory,
/// and return the client and load stats.
let loadAnalyzers () =
    let client = Client<CliAnalyzerAttribute, CliContext>()
    let path = System.IO.Path.GetFullPath(".")
    let stats = client.LoadAnalyzers(path)
    client, stats

/// Assert that the first AnalysisResult from RunAnalyzersSafely
/// contains messages (expectEmpty = false) or is empty (expectEmpty = true).
let assertSafeResult expectEmpty (results: AnalysisResult list) =
    match List.tryHead results with
    | Some result ->
        match result.Output with
        | Ok msgs ->
            if expectEmpty then
                Assert.That(msgs, Is.Empty)
            else
                Assert.That(msgs, Is.Not.Empty)
        | Error ex -> Assert.Fail(sprintf "Expected messages but got exception: %A" ex)
    | None -> Assert.Fail("Expected at least one analyzer result")
