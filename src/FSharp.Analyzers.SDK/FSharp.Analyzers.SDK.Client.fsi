namespace FSharp.Analyzers.SDK

open Microsoft.Extensions.Logging

type AnalysisResult =
    {
        AnalyzerName: string
        /// The messages of the analyzer, or the exception it threw.
        Output: Result<AnalyzerMessage list, exn>
    }

module Client =
    type RegisteredAnalyzer<'TContext when 'TContext :> Context> =
        {
            AssemblyPath: string
            Name: string
            Analyzer: Analyzer<'TContext>
            ShortDescription: string option
            HelpUri: string option
        }

type AssemblyLoadStats =
    {
        /// The number of DLLs from which we tried to load analyzers.
        AnalyzerAssemblies: int
        /// The names of all analyzers loaded across all attempted DLLs.
        AnalyzerNames: string list
        /// The number of assemblies from which we tried and failed to load any DLLs.
        FailedAssemblies: int
    }

    /// The total number of analyzers loaded across all attempted DLLs.
    member Analyzers: int

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
    member LoadAnalyzers: dir: string * ?excludeInclude: ExcludeInclude -> AssemblyLoadStats
    /// <summary>Runs all registered analyzers for given context (file).</summary>
    /// <remarks>Does not throw. An analyzer that throws only fails its own result, the other analyzers keep their messages.</remarks>
    /// <param name="ctx">The context (file) to analyze.</param>
    /// <returns>list of results per analyzer which can either be messages or the exception the analyzer threw.</returns>
    member RunAnalyzers: ctx: 'TContext -> Async<AnalysisResult list>

    /// <summary>Runs all registered analyzers for given context (file).</summary>
    /// <remarks>Does not throw. An analyzer that throws only fails its own result, the other analyzers keep their messages.</remarks>
    /// <param name="ctx">The context (file) to analyze.</param>
    /// <param name="analyzerPredicate">A predicate function to filter which analyzers to run.</param>
    /// <returns>list of results per analyzer which can either be messages or the exception the analyzer threw.</returns>
    member RunAnalyzers:
        ctx: 'TContext * analyzerPredicate: (Client.RegisteredAnalyzer<'TContext> -> bool) ->
            Async<AnalysisResult list>
