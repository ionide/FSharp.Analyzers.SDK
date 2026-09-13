module FSharp.Analyzers.Cli.Tests.SeverityMappingTests

open NUnit.Framework
open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open FSharp.Analyzers.Cli.SeverityMapping

let private parse input =
    match SeverityCode.tryParse input with
    | Ok code -> code
    | Error message -> failwith message

let private emptyMappings () =
    {
        TreatAsInfo = []
        TreatAsHint = []
        TreatAsWarning = []
        TreatAsError = []
    }

let private mkMessage code severity : AnalyzerMessage =
    {
        Message =
            {
                Type = "Test"
                Message = "test message"
                Code = code
                Severity = severity
                Range = Range.range0
                Fixes = []
            }
        Name = "TestAnalyzer"
        AssemblyPath = ""
        ShortDescription = None
        HelpUri = None
    }

let private mapSeverity mappings code severity =
    let mapped = mapMessageToSeverity mappings (mkMessage code severity)
    mapped.Message.Severity

[<Test>]
let ``exact code is parsed as Exact`` () =
    Assert.That(parse "GRA-STRING-001", Is.EqualTo(SeverityCode.Exact "GRA-STRING-001"))

[<Test>]
let ``trailing wildcard is parsed as Prefix`` () =
    Assert.That(parse "GRA-*", Is.EqualTo(SeverityCode.Prefix "GRA-"))

[<Test>]
let ``single wildcard is parsed as empty Prefix`` () =
    Assert.That(parse "*", Is.EqualTo(SeverityCode.Prefix ""))

[<TestCase("GRA-*-001")>]
[<TestCase("*GRA")>]
[<TestCase("GRA-**")>]
let ``wildcard anywhere but the end is rejected`` (input: string) =
    match SeverityCode.tryParse input with
    | Ok code -> Assert.Fail $"Expected an error but got {code}"
    | Error message -> Assert.That(message, Does.Contain input)

[<Test>]
let ``SeverityCode round trips through ToString`` () =
    for input in
        [
            "GRA-001"
            "GRA-*"
            "*"
        ] do
        Assert.That(string (parse input), Is.EqualTo input)

[<TestCase("GRA-001", "GRA-001", true)>]
[<TestCase("GRA-001", "GRA-002", false)>]
[<TestCase("GRA-*", "GRA-001", true)>]
[<TestCase("GRA-*", "IONIDE-001", false)>]
[<TestCase("*", "GRA-001", true)>]
[<TestCase("*", "", true)>]
[<TestCase("gra-*", "GRA-001", false)>]
let ``isMatch`` (pattern: string, code: string, expected: bool) =
    Assert.That(SeverityCode.isMatch code (parse pattern), Is.EqualTo expected)

[<TestCase("*", "GRA-*", true)>]
[<TestCase("GRA-*", "*", true)>]
[<TestCase("GRA-*", "GRA-STRING-*", true)>]
[<TestCase("GRA-*", "IONIDE-*", false)>]
[<TestCase("GRA-001", "GRA-001", true)>]
[<TestCase("GRA-001", "GRA-002", false)>]
[<TestCase("GRA-001", "GRA-*", false)>]
[<TestCase("*", "GRA-001", false)>]
let ``overlaps`` (a: string, b: string, expected: bool) =
    Assert.That(SeverityCode.overlaps (parse a) (parse b), Is.EqualTo expected)

[<Test>]
let ``validation accepts distinct exact codes`` () =
    let mappings =
        { emptyMappings () with
            TreatAsError = [ parse "GRA-001" ]
            TreatAsWarning = [ parse "GRA-002" ]
        }

    match mappings.Validate() with
    | Ok() -> ()
    | Error message -> Assert.Fail message

[<Test>]
let ``validation accepts an exact code next to a pattern that matches it`` () =
    let mappings =
        { emptyMappings () with
            TreatAsError = [ parse "GRA-*" ]
            TreatAsWarning = [ parse "GRA-STRING-001" ]
        }

    match mappings.Validate() with
    | Ok() -> ()
    | Error message -> Assert.Fail message

[<Test>]
let ``validation accepts non overlapping patterns`` () =
    let mappings =
        { emptyMappings () with
            TreatAsError = [ parse "GRA-*" ]
            TreatAsHint = [ parse "IONIDE-*" ]
        }

    match mappings.Validate() with
    | Ok() -> ()
    | Error message -> Assert.Fail message

[<Test>]
let ``validation accepts overlapping patterns in the same list`` () =
    let mappings =
        { emptyMappings () with
            TreatAsError =
                [
                    parse "*"
                    parse "GRA-*"
                ]
        }

    match mappings.Validate() with
    | Ok() -> ()
    | Error message -> Assert.Fail message

[<Test>]
let ``validation rejects the same exact code in two lists`` () =
    let mappings =
        { emptyMappings () with
            TreatAsError = [ parse "GRA-001" ]
            TreatAsWarning = [ parse "GRA-001" ]
        }

    match mappings.Validate() with
    | Ok() -> Assert.Fail "Expected validation to fail"
    | Error message -> Assert.That(message, Does.Contain "'GRA-001'")

[<Test>]
let ``validation rejects overlapping patterns in two lists`` () =
    let mappings =
        { emptyMappings () with
            TreatAsError = [ parse "GRA-*" ]
            TreatAsWarning = [ parse "*" ]
        }

    match mappings.Validate() with
    | Ok() -> Assert.Fail "Expected validation to fail"
    | Error message ->
        Assert.That(message, Does.Contain "'GRA-*'")
        Assert.That(message, Does.Contain "'*'")

[<Test>]
let ``original severity is kept when nothing matches`` () =
    let mappings =
        { emptyMappings () with
            TreatAsError = [ parse "GRA-*" ]
        }

    Assert.That(mapSeverity mappings "IONIDE-001" Severity.Warning, Is.EqualTo Severity.Warning)

[<Test>]
let ``exact code changes the severity`` () =
    let mappings =
        { emptyMappings () with
            TreatAsError = [ parse "GRA-001" ]
        }

    Assert.That(mapSeverity mappings "GRA-001" Severity.Warning, Is.EqualTo Severity.Error)

[<Test>]
let ``wildcard matches every code`` () =
    let mappings =
        { emptyMappings () with
            TreatAsError = [ parse "*" ]
        }

    Assert.That(mapSeverity mappings "GRA-001" Severity.Info, Is.EqualTo Severity.Error)
    Assert.That(mapSeverity mappings "IONIDE-004" Severity.Hint, Is.EqualTo Severity.Error)

[<Test>]
let ``prefix pattern matches its family only`` () =
    let mappings =
        { emptyMappings () with
            TreatAsError = [ parse "GRA-*" ]
        }

    Assert.That(mapSeverity mappings "GRA-STRING-001" Severity.Warning, Is.EqualTo Severity.Error)
    Assert.That(mapSeverity mappings "IONIDE-004" Severity.Warning, Is.EqualTo Severity.Warning)

[<Test>]
let ``exact code takes precedence over a pattern`` () =
    let mappings =
        { emptyMappings () with
            TreatAsError = [ parse "GRA-*" ]
            TreatAsWarning = [ parse "GRA-STRING-001" ]
        }

    Assert.That(mapSeverity mappings "GRA-STRING-001" Severity.Info, Is.EqualTo Severity.Warning)
    Assert.That(mapSeverity mappings "GRA-STRING-002" Severity.Info, Is.EqualTo Severity.Error)

[<Test>]
let ``exact code takes precedence regardless of list order`` () =
    let mappings =
        { emptyMappings () with
            TreatAsInfo = [ parse "*" ]
            TreatAsError = [ parse "GRA-001" ]
        }

    Assert.That(mapSeverity mappings "GRA-001" Severity.Warning, Is.EqualTo Severity.Error)
    Assert.That(mapSeverity mappings "GRA-002" Severity.Warning, Is.EqualTo Severity.Info)
