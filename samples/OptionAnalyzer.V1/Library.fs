module OptionAnalyzer.V1

open FSharp.Analyzers.SDK.V1
open FSharp.Analyzers.SDK.V1.TASTCollecting

let rec private findOptionValue (expr: TypedExpr) (results: ResizeArray<SourceRange>) =
    match expr with
    | TypedExpr.Call(_, m, _, _, _, range) ->
        m.DeclaringEntity
        |> Option.iter (fun de ->
            let name = System.String.Join(".", de.FullName, m.DisplayName)

            if name = "Microsoft.FSharp.Core.FSharpOption`1.Value" then
                results.Add range
        )
    | _ -> ()

[<CliAnalyzer "OptionAnalyzer">]
let optionValueAnalyzer: Analyzer =
    fun ctx ->
        async {
            let results = ResizeArray()

            match ctx.TypedTree with
            | None -> ()
            | Some handle ->
                let tree = convertTast handle
                visitTypedTree (fun expr -> findOptionValue expr results) tree

            return
                results
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
