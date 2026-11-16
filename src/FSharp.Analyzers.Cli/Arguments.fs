namespace FSharp.Analyzers.Cli

open System
open System.Globalization
open Argu

module ArgumentDefaults =

    [<Literal>]
    let MaxConcurrentScripts = 4

type Arguments =
    | Project of string list
    | Script of string list
    | Define of string list
    | [<Unique>] Max_Concurrent_Scripts of int
    | Analyzers_Path of string list
    | [<EqualsAssignment; AltCommandLine("-p:"); AltCommandLine("-p")>] Property of string * string
    | [<Unique; AltCommandLine("-c")>] Configuration of string
    | [<Unique; AltCommandLine("-r")>] Runtime of string
    | [<Unique; AltCommandLine("-a")>] Arch of string
    | [<Unique>] Os of string
    | Treat_As_Info of string list
    | Treat_As_Hint of string list
    | Treat_As_Warning of string list
    | Treat_As_Error of string list
    | Exclude_Files of string list
    | Include_Files of string list
    | Exclude_Analyzers of string list
    | Include_Analyzers of string list
    | [<Unique>] Report of string
    | [<Unique>] FSC_Args of string
    | [<Unique>] FSC_Args_File of string
    | [<Unique>] Code_Root of string
    | [<Unique; AltCommandLine("-v")>] Verbosity of string
    | [<Unique>] Output_Format of string
    | [<Unique>] BinLog_Path of string
    | [<Unique>] Fail_On_Type_Errors

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Project _ ->
                "List of paths to your .fsproj file. Cannot be combined with `--fsc-args`."
            | Script _ ->
                "List of paths to your .fsx file. Supports globs. Each path or glob must match at least one file. Cannot be combined with `--fsc-args`."
            | Define _ ->
                "List of conditional compilation symbols to define when checking the `--script` files, one symbol per value, like `--define` of `dotnet fsi`. Does not apply to `--project`, which gets its symbols from `DefineConstants`."
            | Max_Concurrent_Scripts _ ->
                $"The maximum number of `--script` files to check at the same time. The default is %d{ArgumentDefaults.MaxConcurrentScripts}. A higher value can be faster and uses more memory."
            | Analyzers_Path _ ->
                "List of path to a folder where your analyzers are located. This will search recursively."
            | Property _ -> "A key=value pair of an MSBuild property."
            | Configuration _ -> "The configuration to use, e.g. Debug or Release."
            | Runtime _ -> "The runtime identifier (RID)."
            | Arch _ -> "The target architecture."
            | Os _ -> "The target operating system."
            | Treat_As_Info _ ->
                "List of analyzer codes that should be treated as severity Info by the tool. Regardless of the original severity. Supports a trailing `*` wildcard, e.g. `GRA-*` or `*`. An exact code takes precedence over a pattern."
            | Treat_As_Hint _ ->
                "List of analyzer codes that should be treated as severity Hint by the tool. Regardless of the original severity. Supports a trailing `*` wildcard, e.g. `GRA-*` or `*`. An exact code takes precedence over a pattern."
            | Treat_As_Warning _ ->
                "List of analyzer codes that should be treated as severity Warning by the tool. Regardless of the original severity. Supports a trailing `*` wildcard, e.g. `GRA-*` or `*`. An exact code takes precedence over a pattern."
            | Treat_As_Error _ ->
                "List of analyzer codes that should be treated as severity Error by the tool. Regardless of the original severity. Supports a trailing `*` wildcard, e.g. `GRA-*` or `*`. An exact code takes precedence over a pattern."
            | Exclude_Files _ -> "Source files that shouldn't be processed."
            | Include_Files _ ->
                "Source files that should be processed exclusively while all others are ignored. Takes precedence over --exclude-files."
            | Exclude_Analyzers _ -> "The names of analyzers that should not be executed."
            | Include_Analyzers _ ->
                "The names of analyzers that should exclusively be executed while all others are ignored. Takes precedence over --exclude-analyzers."
            | Report _ -> "Write the result messages to a (sarif) report file."
            | Verbosity _ ->
                "The verbosity level. The available verbosity levels are: n[ormal], d[etailed], diag[nostic]."
            | FSC_Args _ ->
                "Pass in the raw fsc compiler arguments. Cannot be combined with the `--project` flag."
            | FSC_Args_File _ ->
                "Path to a response (RSP) file containing fsc compiler arguments. Cannot be combined with `--project` or `--fsc-args` flags."
            | Code_Root _ ->
                "Root of the current code repository, used in the sarif report to construct the relative file path. The current working directory is used by default."
            | Output_Format _ ->
                "Format in which to write analyzer results to stdout. The available options are: default, github."
            | BinLog_Path(_) ->
                "Path to a directory where MSBuild binary logs (binlog) will be written. You can use https://msbuildlog.com/ to view them."
            | Fail_On_Type_Errors ->
                "Exit with a non-zero code when a project or script does not type check. Analyzers using the typed tree cannot see code that failed to type check, so the run is incomplete."

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Arguments =

    /// The value of `--max-concurrent-scripts`, or its default. The error is a value below 1.
    let maxConcurrentScripts (results: ParseResults<Arguments>) : Result<int, int> =
        match results.TryGetResult <@ Max_Concurrent_Scripts @> with
        | None -> Ok ArgumentDefaults.MaxConcurrentScripts
        | Some count when count >= 1 -> Ok count
        | Some count -> Error count

    /// Argu's `GetResult` only returns the last occurrence of a repeated flag and silently drops the earlier ones.
    /// List-valued flags accumulate instead, so `--script a --script b` means the same as `--script a b`.
    let getAll
        (results: ParseResults<Arguments>)
        (argument: Quotations.Expr<string list -> Arguments>)
        =
        results.GetResults argument
        |> List.concat

    /// The `--define` values. The error lists each value that is not one conditional compilation symbol.
    let defines (results: ParseResults<Arguments>) : Result<string list, string list> =
        // The identifier rules of the F# preprocessor lexer, pplex.fsl in dotnet/fsharp.
        let isLetter (c: char) =
            match Char.GetUnicodeCategory c with
            | UnicodeCategory.UppercaseLetter
            | UnicodeCategory.LowercaseLetter
            | UnicodeCategory.TitlecaseLetter
            | UnicodeCategory.ModifierLetter
            | UnicodeCategory.OtherLetter
            | UnicodeCategory.LetterNumber -> true
            | _ -> false

        let isSymbolChar (c: char) =
            match Char.GetUnicodeCategory c with
            | _ when isLetter c || c = '\'' -> true
            | UnicodeCategory.ConnectorPunctuation
            | UnicodeCategory.NonSpacingMark
            | UnicodeCategory.SpacingCombiningMark
            | UnicodeCategory.Format
            | UnicodeCategory.DecimalDigitNumber -> true
            | _ -> false

        let isSymbol (value: string) =
            value.Length > 0
            && (isLetter value[0]
                || value[0] = '_')
            && Seq.forall isSymbolChar value

        let defines = getAll results <@ Define @>

        match List.filter (isSymbol >> not) defines with
        | [] -> Ok defines
        | invalid -> Error invalid
