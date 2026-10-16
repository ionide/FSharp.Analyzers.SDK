namespace FSharp.Analyzers.Cli

open FSharp.Compiler.Diagnostics

/// A project or script that did not type check.
/// A type error is replaced with an error recovery node in the typed tree,
/// so analyzers using the typed tree cannot see the code these errors are in.
type TypeCheckFailure =
    {
        ProjectFileName: string
        Errors: FSharpDiagnostic list
    }

/// An analyzer that threw while analyzing a file, so its messages for that file are missing.
type AnalyzerFailure =
    {
        AnalyzerName: string
        FileName: string
        Exception: exn
    }
