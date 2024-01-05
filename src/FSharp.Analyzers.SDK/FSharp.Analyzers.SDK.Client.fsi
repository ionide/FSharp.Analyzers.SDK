namespace FSharp.Analyzers.SDK

open Microsoft.Extensions.Logging

type AnalysisResult =
    {
        AnalyzerName: string
        Output: Result<Message list, exn>
    }

type ExcludeInclude =
    /// A predicate function to exclude Analyzers.
    | ExcludeFilter of (string -> bool)
    /// A predicate function to include Analyzers exclusively, while all others are ignored.
    | IncludeFilter of (string -> bool)

type Client<'TAttribute, 'TContext when 'TAttribute :> AnalyzerAttribute and 'TContext :> Context> =
    new: logger: ILogger -> Client<'TAttribute, 'TContext>
    new: unit -> Client<'TAttribute, 'TContext>
    /// <summary>
    /// Loads into private state any analyzers defined in any assembly
    /// matching `*Analyzer*.dll` in given directory (and any subdirectories)
    /// Analyzers are filtered according to the ExcludeInclude set, if provided.
    /// </summary>
    /// <returns>number of found dlls matching `*Analyzer*.dll` and number of registered analyzers</returns>
    member LoadAnalyzers: dir: string * ?excludeInclude: ExcludeInclude -> int * int
    /// <summary>Runs all registered analyzers for given context (file).</summary>
    /// <returns>list of messages. Ignores errors from the analyzers</returns>
    member RunAnalyzers: ctx: 'TContext -> Async<AnalyzerMessage list>
    /// <summary>Runs all registered analyzers for given context (file).</summary>
    /// <returns>list of results per analyzer which can either be messages or an exception.</returns>
    member RunAnalyzersSafely: ctx: 'TContext -> Async<AnalysisResult list>
