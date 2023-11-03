module OptionAnalyzer

open System
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Text

let notUsed () =
    let option: Option<int> = None
    option.Value

[<CliAnalyzer "OptionAnalyzer">]
let optionValueAnalyzer: Analyzer<CliContext> =
    fun ctx ->
        async {
            let state = ResizeArray<range>()

            let walker =
                { new TypedTreeCollectorBase() with
                    override _.WalkCall range m _ =
                        let name = String.Join(".", m.DeclaringEntity.Value.FullName, m.DisplayName)

                        if name = "Microsoft.FSharp.Core.FSharpOption`1.Value" then
                            state.Add range

                }

            match ctx.TypedTree with
            | None -> ()
            | Some typedTree -> typedTree.Declarations |> List.iter (walkTast walker)

            return
                state
                |> Seq.map (fun r ->
                    {
                        Type = "Option.Value analyzer"
                        Message = "Option.Value shouldn't be used"
                        Code = "OV001"
                        Severity = Warning
                        Range = r
                        Fixes = []
                    }
                )
                |> Seq.toList
        }
