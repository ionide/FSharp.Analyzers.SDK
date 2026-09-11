module FSharp.Analyzers.Cli.MsBuildProperties

open System
open System.Runtime.InteropServices
open System.Text.RegularExpressions
open Argu
open Microsoft.Extensions.Logging

/// If multiple MSBuild properties are given in one -p flag like -p:prop1="val1a;val1b;val1c";prop2="1;2;3";prop3=val3
/// argu will think it means prop1 has the value: "val1a;val1b;val1c";prop2="1;2;3";prop3=val3
/// so this function expands the value into multiple key-value properties
// built once for every property, not per element
let private multiPropertyRegex = Regex(";([a-z,A-Z,0-9,_,-]*)=")

let expandMultiProperties (logger: ILogger) (properties: (string * string) list) =
    properties
    |> List.map (fun (k, v) ->
        if not (v.Contains('=')) then // no multi properties given to expand
            [ (k, v) ]
        else
            let splits = multiPropertyRegex.Split(v)

            [
                yield (k, splits[0])

                for pair in
                    splits.[1..]
                    |> Seq.chunkBySize 2 do
                    match pair with
                    | [| k; v |] when String.IsNullOrWhiteSpace(v) ->
                        logger.LogError("Missing property value for '{0}'", k)
                        exit (int ExitErrorCodes.MissingPropertyValue)
                    | [| k; v |] -> yield (k, v)
                    | _ -> ()

            ]
    )
    |> List.concat

let validateRuntimeOsArchCombination (logger: ILogger) (runtime, arch, os) =
    match runtime, os, arch with
    | Some _, Some _, _ ->
        logger.LogError("Specifying both the `-r|--runtime` and `-os` options is not supported.")
        exit (int ExitErrorCodes.RuntimeAndOsOptions)
    | Some _, _, Some _ ->
        logger.LogError(
            "Specifying both the `-r|--runtime` and `-a|--arch` options is not supported."
        )

        exit (int ExitErrorCodes.RuntimeAndArchOptions)
    | _ -> ()

let getProperties (logger: ILogger) (results: ParseResults<Arguments>) =
    let runtime = results.TryGetResult <@ Runtime @>
    let arch = results.TryGetResult <@ Arch @>
    let os = results.TryGetResult <@ Os @>
    validateRuntimeOsArchCombination logger (runtime, os, arch)

    let runtimeProp =
        let rid = RuntimeInformation.RuntimeIdentifier // assuming we always get something like 'linux-x64'

        match runtime, os, arch with
        | Some r, _, _ -> Some r
        | None, Some o, Some a -> Some $"{o}-{a}"
        | None, Some o, None ->
            let archOfRid = rid.Substring(rid.LastIndexOf('-') + 1)

            Some $"{o}-{archOfRid}"
        | None, None, Some a ->
            let osOfRid = rid.Substring(0, rid.LastIndexOf('-'))
            Some $"{osOfRid}-{a}"
        | _ -> None

    results.GetResults <@ Property @>
    |> expandMultiProperties logger
    |> fun props ->
        [
            yield! props

            match results.TryGetResult <@ Configuration @> with
            | (Some x) -> yield ("Configuration", x)
            | _ -> ()

            match runtimeProp with
            | (Some x) -> yield ("RuntimeIdentifier", x)
            | _ -> ()
        ]
