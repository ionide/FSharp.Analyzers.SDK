module OptionAnalyzer.Test

#nowarn "57"

open NUnit.Framework
open FSharp.Compiler.Text
open FSharp.Analyzers.SDK

[<SetUp>]
let Setup () = ()

[<Test>]
let ``warning is emitted`` () =

    let source =
        """
module M

let notUsed() =
    let option : Option<int> = None
    option.Value
"""

    let expectedMsg =
        { Code = "OV001"
          Fixes = []
          Message = "Option.Value shouldn't be used"
          Range = Range.mkRange "A.fs" (Position.mkPos 6 4) (Position.mkPos 6 16)
          Severity = Severity.Warning
          Type = "Option.Value analyzer" }

    let msgs = TestHelpers.getAnalyzerMsgs source
    Assert.IsNotEmpty msgs
    Assert.IsTrue(msgs |> Array.contains expectedMsg)
