module OptionAnalyzer

open System
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Text

let notUsed () =
    let option: Option<int> = None
    option.Value

let handler typeTree =
    async {
        let state = ResizeArray<range>()

        let walker =
            { new TypedTreeCollectorBase() with
                override _.WalkCall _ m _ _ _ range =
                    m.DeclaringEntity
                    |> Option.iter (fun de ->
                        let name = String.Join(".", de.FullName, m.DisplayName)

                        if name = "Microsoft.FSharp.Core.FSharpOption`1.Value" then
                            state.Add range
                    )

            }

        match typeTree with
        | None -> ()
        | Some typedTree -> walkTast walker typedTree

        return
            state
            |> Seq.map (fun r ->
                {
                    Type = "Option.Value analyzer"
                    Message = "Option.Value shouldn't be used"
                    Code = "OV001"
                    Severity = Severity.Warning
                    Range = r
                    Fixes = []
                }
            )
            |> Seq.toList
    }


[<EditorAnalyzerAttribute "OptionAnalyzer">]
let analyzerEditorContext : Analyzer<EditorContext> =
    fun ctx ->  handler ctx.TypedTree

[<CliAnalyzer "OptionAnalyzer">]
let optionValueAnalyzer: Analyzer<CliContext> =
    fun ctx ->  handler ctx.TypedTree
