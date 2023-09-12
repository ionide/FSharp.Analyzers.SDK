module OptionAnalyzer.Test

open FSharp.Compiler.CodeAnalysis
open NUnit.Framework
open FSharp.Compiler.Text
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TestHelpers

let mutable projectOptions: FSharpProjectOptions = FSharpProjectOptions.zero

[<SetUp>]
let Setup () =
    projectOptions <-
        mkOptionsFromProject
            DotNetVersion.Seven
            (Some
                {
                    Name = "Newtonsoft.Json"
                    Version = "13.0.3"
                })

[<Test>]
let ``warnings are emitted`` () =

    let source =
        """
module M

let notUsed() =
    let option : Option<int> = None
    option.Value
"""

    let ctx = getContext projectOptions source
    let msgs = optionValueAnalyzer ctx
    Assert.IsNotEmpty msgs

[<Test>]
let ``expected warning is emitted`` () =

    let source =
        """
module M

open Newtonsoft.Json

let json = JsonConvert.SerializeObject([1;2;3])

let notUsed() =
    let option : Option<int> = None
    option.Value
"""

    let expectedMsg =
        {
            Code = "OV001"
            Fixes = []
            Message = "Option.Value shouldn't be used"
            Range = Range.mkRange "A.fs" (Position.mkPos 10 4) (Position.mkPos 10 16)
            Severity = Severity.Warning
            Type = "Option.Value analyzer"
        }

    let ctx = getContext projectOptions source
    let msgs = optionValueAnalyzer ctx
    Assert.IsTrue(msgs |> List.contains expectedMsg)
