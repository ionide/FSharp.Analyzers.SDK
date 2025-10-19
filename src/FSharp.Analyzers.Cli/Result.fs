module FSharp.Analyzers.Cli.Result

let allOkOrError<'ok, 'err>
    (results: Result<'ok, 'err> list)
    : Result<'ok list, 'ok list * 'err list>
    =
    let oks, errs =
        (([], []), results)
        ||> List.fold (fun (oks, errs) result ->
            match result with
            | Ok ok -> ok :: oks, errs
            | Error err ->
                oks,
                err
                :: errs
        )

    let oks = List.rev oks
    let errs = List.rev errs

    if List.isEmpty errs then Ok oks else Error(oks, errs)
