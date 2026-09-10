namespace FSharp.Analyzers.Cli

type ExitErrorCodes =
    | Success = 0
    | NoAnalyzersFound = -1
    | AnalyzerFoundError = -2
    | FailedAssemblyLoading = -3
    | AnalysisAborted = -4
    | FailedToLoadProject = 10
    | EmptyFscArgs = 11
    | MissingPropertyValue = 12
    | RuntimeAndOsOptions = 13
    | RuntimeAndArchOptions = 14
    | UnknownLoggerVerbosity = 15
    | AnalyzerListedMultipleTimesInTreatAsSeverity = 16
    | FscArgsCombinedWithMsBuildProperties = 17
    | FSharpCoreAssemblyLoadFailed = 18
    | ProjectAndFscArgs = 19
    | InvalidScriptArguments = 20
    | InvalidProjectArguments = 21
    | InvalidTreatAsSeverityPattern = 23
    | UnhandledException = 22
