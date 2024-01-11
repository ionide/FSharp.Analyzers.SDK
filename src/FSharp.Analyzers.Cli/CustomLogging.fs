module FSharp.Analyzers.Cli.CustomLogging

open System
open System.IO
open System.Runtime.CompilerServices
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Console
open Microsoft.Extensions.Logging.Abstractions
open Microsoft.Extensions.Options

module AnsiColorHelpers =
    let private initialConsoleColor = Console.ForegroundColor
    // see https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter#implement-custom-color-formatting
    let private ansiForegroundEscapeCodeOfColorConsole color =
        match color with
        | ConsoleColor.Black -> "\x1B[30m"
        | ConsoleColor.DarkRed -> "\x1B[31m"
        | ConsoleColor.DarkGreen -> "\x1B[32m"
        | ConsoleColor.DarkYellow -> "\x1B[33m"
        | ConsoleColor.DarkBlue -> "\x1B[34m"
        | ConsoleColor.DarkMagenta -> "\x1B[35m"
        | ConsoleColor.DarkCyan -> "\x1B[36m"
        | ConsoleColor.Gray -> "\x1B[37m"
        | ConsoleColor.Red -> "\x1B[1m\x1B[31m"
        | ConsoleColor.Green -> "\x1B[1m\x1B[32m"
        | ConsoleColor.Yellow -> "\x1B[1m\x1B[33m"
        | ConsoleColor.Blue -> "\x1B[1m\x1B[34m"
        | ConsoleColor.Magenta -> "\x1B[1m\x1B[35m"
        | ConsoleColor.Cyan -> "\x1B[1m\x1B[36m"
        | ConsoleColor.White -> "\x1B[1m\x1B[37m"
        | _ ->
#if DEBUG
            failwith $"didn't implement ansi code for color: {color}"
#else
            // do not break code analyzis to wrong runtime color or such thing for release
            "\x1B[37m" // ConsoleColor.Gray
#endif

    let consoleColorOfLogLevel logLevel =
        match logLevel with
        | LogLevel.Error -> ConsoleColor.Red
        | LogLevel.Warning -> ConsoleColor.DarkYellow
        | LogLevel.Information -> ConsoleColor.Blue
        | LogLevel.Trace -> ConsoleColor.Cyan
        | _ -> ConsoleColor.Gray

    let formatMessageAsAnsiColorizedString (color: ConsoleColor) (message: string) =
        $"{ansiForegroundEscapeCodeOfColorConsole color}{message}{ansiForegroundEscapeCodeOfColorConsole initialConsoleColor}"

type CustomOptions() =
    inherit ConsoleFormatterOptions()

    /// if true: no LogLevel as prefix, colored output according to LogLevel
    /// if false: LogLevel as prefix, no colored output
    member val UseAnalyzersMsgStyle = false with get, set
    member x.UseLogLevelAsPrefix = not x.UseAnalyzersMsgStyle

type CustomFormatter(options: IOptionsMonitor<CustomOptions>) as this =
    inherit ConsoleFormatter("customName")

    let mutable optionsReloadToken: IDisposable = null
    let mutable formatterOptions = options.CurrentValue
    do optionsReloadToken <- options.OnChange(fun x -> this.ReloadLoggerOptions(x))

    member private _.ReloadLoggerOptions(opts: CustomOptions) = formatterOptions <- opts

    override this.Write<'TState>
        (
            logEntry: inref<LogEntry<'TState>>,
            _scopeProvider: IExternalScopeProvider,
            textWriter: TextWriter
        )
        =
        let message = logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception)

        if formatterOptions.UseLogLevelAsPrefix then
            this.WritePrefix(textWriter, logEntry.LogLevel)

        textWriter.WriteLine message

    member private _.WritePrefix(textWriter: TextWriter, logLevel: LogLevel) =
        match logLevel with
        | LogLevel.Trace -> textWriter.Write("trace: ")
        | LogLevel.Debug -> textWriter.Write("debug: ")
        | LogLevel.Information -> textWriter.Write("info: ")
        | LogLevel.Warning -> textWriter.Write("warn: ")
        | LogLevel.Error -> textWriter.Write("error: ")
        | LogLevel.Critical -> textWriter.Write("critical: ")
        | _ -> ()

    interface IDisposable with
        member _.Dispose() = optionsReloadToken.Dispose()

[<Extension>]
type ConsoleLoggerExtensions =

    [<Extension>]
    static member AddCustomFormatter(builder: ILoggingBuilder, configure: Action<CustomOptions>) : ILoggingBuilder =
        builder
            .AddConsole(fun options -> options.FormatterName <- "customName")
            .AddConsoleFormatter<CustomFormatter, CustomOptions>(configure)
