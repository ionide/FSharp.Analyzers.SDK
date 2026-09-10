module FSharp.Analyzers.Cli.SeverityMapping

open System
open FSharp.Analyzers.SDK

/// An entry of a `--treat-as-*` argument: either an exact analyzer code or a pattern with a trailing `*`.
[<RequireQualifiedAccess>]
type SeverityCode =
    /// Matches the code exactly, e.g. `GRA-STRING-001`.
    | Exact of string
    /// Matches every code starting with the prefix, e.g. `GRA-*` (prefix `GRA-`) or `*` (empty prefix).
    | Prefix of string

    override x.ToString() =
        match x with
        | SeverityCode.Exact code -> code
        | SeverityCode.Prefix prefix -> $"{prefix}*"

module SeverityCode =
    /// Only a single trailing `*` is supported as wildcard.
    let tryParse (input: string) : Result<SeverityCode, string> =
        let firstStar = input.IndexOf '*'

        if firstStar = -1 then
            Ok(SeverityCode.Exact input)
        elif firstStar = input.Length - 1 then
            Ok(SeverityCode.Prefix(input.Substring(0, firstStar)))
        else
            Error
                $"Invalid analyzer code pattern '{input}': a '*' wildcard is only allowed at the end."

    let isMatch (code: string) (severityCode: SeverityCode) =
        match severityCode with
        | SeverityCode.Exact exact -> String.Equals(code, exact, StringComparison.Ordinal)
        | SeverityCode.Prefix prefix -> code.StartsWith(prefix, StringComparison.Ordinal)

    /// Two prefix patterns overlap when one is a prefix of the other, e.g. `*` and `GRA-*`.
    let overlaps (a: SeverityCode) (b: SeverityCode) =
        match a, b with
        | SeverityCode.Prefix x, SeverityCode.Prefix y ->
            x.StartsWith(y, StringComparison.Ordinal)
            || y.StartsWith(x, StringComparison.Ordinal)
        | _ -> a = b

type SeverityMappings =
    {
        TreatAsInfo: SeverityCode list
        TreatAsHint: SeverityCode list
        TreatAsWarning: SeverityCode list
        TreatAsError: SeverityCode list
    }

    member x.Mappings =
        [
            x.TreatAsInfo, Severity.Info
            x.TreatAsHint, Severity.Hint
            x.TreatAsWarning, Severity.Warning
            x.TreatAsError, Severity.Error
        ]

    /// An exact code may only appear in one list and prefix patterns from different lists may not overlap.
    /// An exact code listed next to a pattern that also matches it is allowed: the exact code takes precedence.
    member x.Validate() : Result<unit, string> =
        let conflicts =
            [
                for i, (codesA, severityA) in List.indexed x.Mappings do
                    for j, (codesB, severityB) in List.indexed x.Mappings do
                        if i < j then
                            for a in codesA do
                                for b in codesB do
                                    if SeverityCode.overlaps a b then
                                        yield
                                            $"'{a}' (treat as {severityA}) and '{b}' (treat as {severityB})"
            ]

        match conflicts with
        | [] -> Ok()
        | conflicts ->
            let details =
                conflicts
                |> String.concat ", "

            Error
                $"An analyzer code may only be listed once in the <treat-as-severity> arguments and patterns may not overlap. Conflicts: {details}."

let mapMessageToSeverity (mappings: SeverityMappings) (msg: FSharp.Analyzers.SDK.AnalyzerMessage) =
    let code = msg.Message.Code

    let tryFind (predicate: SeverityCode -> bool) =
        mappings.Mappings
        |> List.tryPick (fun (codes, severity) ->
            if List.exists predicate codes then Some severity else None
        )

    let exactMatch =
        tryFind (
            function
            | SeverityCode.Exact _ as c -> SeverityCode.isMatch code c
            | SeverityCode.Prefix _ -> false
        )

    let prefixMatch () =
        tryFind (
            function
            | SeverityCode.Prefix _ as c -> SeverityCode.isMatch code c
            | SeverityCode.Exact _ -> false
        )

    let targetSeverity =
        // An exact code takes precedence over a pattern.
        match exactMatch with
        | Some severity -> severity
        | None ->
            prefixMatch ()
            |> Option.defaultValue msg.Message.Severity

    { msg with
        Message =
            { msg.Message with
                Severity = targetSeverity
            }
    }
