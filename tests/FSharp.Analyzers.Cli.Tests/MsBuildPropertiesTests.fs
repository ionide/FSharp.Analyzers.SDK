module FSharp.Analyzers.Cli.Tests.MsBuildPropertiesTests

open NUnit.Framework
open Microsoft.Extensions.Logging.Abstractions
open FSharp.Analyzers.Cli.MsBuildProperties

let private expand properties =
    expandMultiProperties NullLogger.Instance properties

[<Test>]
let ``single property is kept as is`` () =
    Assert.That(expand [ "Configuration", "Release" ], Is.EqualTo [ "Configuration", "Release" ])

[<Test>]
let ``multiple properties in one value are expanded`` () =
    let actual = expand [ "prop1", "val1a;val1b;val1c\";prop2=\"1;2;3\";prop3=val3" ]

    let expected =
        [
            "prop1", "val1a;val1b;val1c\""
            "prop2", "\"1;2;3\""
            "prop3", "val3"
        ]

    Assert.That(actual, Is.EqualTo expected)

[<Test>]
let ``properties from separate flags are all kept`` () =
    let actual =
        expand
            [
                "a", "1"
                "b", "2"
            ]

    Assert.That(
        actual,
        Is.EqualTo
            [
                "a", "1"
                "b", "2"
            ]
    )
