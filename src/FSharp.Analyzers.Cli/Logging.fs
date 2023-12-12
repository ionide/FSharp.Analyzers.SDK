namespace Console.ExampleFormatters.Custom

open System
open System.IO
open System.Runtime.CompilerServices
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Console
open Microsoft.Extensions.Logging.Abstractions
open Microsoft.Extensions.Options

type CustomOptions() =
    inherit ConsoleFormatterOptions()

    member val CustomPrefix = "" with get, set

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
        this.CustomLogic(textWriter)
        textWriter.WriteLine(message)

    member private _.CustomLogic(textWriter: TextWriter) =
        textWriter.Write(formatterOptions.CustomPrefix)

    interface IDisposable with
        member _.Dispose() = optionsReloadToken.Dispose()

[<Extension>]
type ConsoleLoggerExtensions =

    [<Extension>]
    static member AddCustomFormatter(builder: ILoggingBuilder, configure: Action<CustomOptions>) : ILoggingBuilder =
        builder
            .AddConsole(fun options -> options.FormatterName <- "customName")
            .AddConsoleFormatter<CustomFormatter, CustomOptions>(configure)
