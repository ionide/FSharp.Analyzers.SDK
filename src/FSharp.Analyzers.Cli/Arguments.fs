namespace FSharp.Analyzers.Cli

open Argu

type Arguments =
    | Project of string list
    | Script of string list
    | Analyzers_Path of string list
    | [<EqualsAssignment; AltCommandLine("-p:"); AltCommandLine("-p")>] Property of string * string
    | [<Unique; AltCommandLine("-c")>] Configuration of string
    | [<Unique; AltCommandLine("-r")>] Runtime of string
    | [<Unique; AltCommandLine("-a")>] Arch of string
    | [<Unique>] Os of string
    | [<Unique>] Treat_As_Info of string list
    | [<Unique>] Treat_As_Hint of string list
    | [<Unique>] Treat_As_Warning of string list
    | [<Unique>] Treat_As_Error of string list
    | [<Unique>] Exclude_Files of string list
    | [<Unique>] Include_Files of string list
    | [<Unique>] Exclude_Analyzers of string list
    | [<Unique>] Include_Analyzers of string list
    | [<Unique>] Report of string
    | [<Unique>] FSC_Args of string
    | [<Unique>] FSC_Args_File of string
    | [<Unique>] Code_Root of string
    | [<Unique; AltCommandLine("-v")>] Verbosity of string
    | [<Unique>] Output_Format of string
    | [<Unique>] BinLog_Path of string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Project _ ->
                "List of paths to your .fsproj file. Cannot be combined with `--fsc-args`."
            | Script _ ->
                "List of paths to your .fsx file. Supports globs. Cannot be combined with `--fsc-args`."
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
