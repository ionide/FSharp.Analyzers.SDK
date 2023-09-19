module OptionAnalyzer.Test

open FSharp.Compiler.CodeAnalysis
open NUnit.Framework
open FSharp.Compiler.Text
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.Testing

let mutable projectOptions: FSharpProjectOptions = FSharpProjectOptions.zero

[<SetUp>]
let Setup () =
    task {
        let! opts =
            mkOptionsFromProject
                "net7.0"
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

        projectOptions <- opts
    }

[<Test>]
let ``warnings are emitted`` () =
    async {
        let source =
            """
module M

let notUsed() =
    let option : Option<int> = None
    option.Value
    """

        let ctx = getContext projectOptions source
        let! msgs = optionValueAnalyzer ctx
        Assert.IsNotEmpty msgs
        Assert.IsTrue(Assert.messageContains "Option.Value" msgs[0])

    }

[<Test>]
let ``expected warning is emitted`` () =
    async {
        let source =
            """
module M

open Newtonsoft.Json
open Fantomas.FCS

let json = JsonConvert.SerializeObject([1;2;3])

let p = Fantomas.FCS.Text.Position.mkPos 23 2

let notUsed() =
    let option : Option<int> = None
    option.Value
    """

        let expectedMsg =
            {
                Code = "OV001"
                Fixes = []
                Message = "Option.Value shouldn't be used"
                Range = Range.mkRange "A.fs" (Position.mkPos 13 4) (Position.mkPos 13 16)
                Severity = Severity.Warning
                Type = "Option.Value analyzer"
            }

        let ctx = getContext projectOptions source
        let! msgs = optionValueAnalyzer ctx
        Assert.IsTrue(msgs |> List.contains expectedMsg)
    }
