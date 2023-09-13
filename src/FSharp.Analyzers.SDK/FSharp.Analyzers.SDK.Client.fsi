namespace FSharp.Analyzers.SDK

open System.Collections.Concurrent

type AnalysisResult =
    {
        AnalyzerName: string
        Output: Result<Message list, exn>
    }

module Client =
    val registeredAnalyzers: ConcurrentDictionary<string, (string * Analyzer) list>
    /// <summary>
    /// Loads into private state any analyzers defined in any assembly
    /// matching `*Analyzer*.dll` in given directory (and any subdirectories)
    /// </summary>
    /// <returns>number of found dlls matching `*Analyzer*.dll` and number of registered analyzers</returns>
    val loadAnalyzers: printError: (string -> unit) -> dir: string -> int * int
    /// <summary>Runs all registered analyzers for given context (file).</summary>
    /// <returns>list of messages. Ignores errors from the analyzers</returns>
    val runAnalyzers: ctx: Context -> Message array
    /// <summary>Runs all registered analyzers for given context (file).</summary>
    /// <returns>list of results per analyzer which can ei</returns>
    val runAnalyzersSafely: ctx: Context -> AnalysisResult list
