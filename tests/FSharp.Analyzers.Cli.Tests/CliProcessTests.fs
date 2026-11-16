module FSharp.Analyzers.Cli.Tests.CliProcessTests

open System
open System.Diagnostics
open System.IO
open System.Threading
open NUnit.Framework
open FSharp.Analyzers.Cli
open FSharp.Analyzers.Cli.Tests.AnalyzerLoadingTests

/// Runs the CLI of this build with the arguments and returns its exit code and output.
/// The CLI runs in the test output folder, so the global.json of the repository selects the SDK.
let private runCli (arguments: string list) =
    let cli = typeof<Arguments>.Assembly.Location
    let startInfo = ProcessStartInfo("dotnet")
    startInfo.ArgumentList.Add cli

    for argument in arguments do
        startInfo.ArgumentList.Add argument

    startInfo.WorkingDirectory <- Path.GetDirectoryName cli
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.UseShellExecute <- false

    use cliProcess = Process.Start startInfo
    let output = cliProcess.StandardOutput.ReadToEndAsync()
    let error = cliProcess.StandardError.ReadToEndAsync()
    use deadline = new CancellationTokenSource(TimeSpan.FromMinutes 3.0)

    try
        cliProcess.WaitForExitAsync(deadline.Token).GetAwaiter().GetResult()
    with :? OperationCanceledException ->
        cliProcess.Kill true
        cliProcess.WaitForExit()

        Assert.Fail
            $"The CLI did not exit within 3 minutes. Output: %s{output.Result}%s{error.Result}"

    cliProcess.ExitCode, output.Result + error.Result

/// Exit codes are taken modulo 256 on Linux and macOS.
let private exitCodeOf (code: ExitErrorCodes) = int code &&& 0xFF

let private withFolder (f: string -> 'a) =
    let folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())

    Directory.CreateDirectory folder
    |> ignore

    try
        f folder
    finally
        try
            Directory.Delete(folder, true)
        with _ ->
            ()

let private writeScript folder (source: string) =
    let script = Path.Combine(folder, "script.fsx")
    File.WriteAllText(script, source)
    script

let private compileAnalyzer folder =
    let analyzersDir = Path.Combine(folder, "analyzers")

    Directory.CreateDirectory analyzersDir
    |> ignore

    compile
        []
        (Path.Combine(analyzersDir, "EmptyAnalyzer.dll"))
        """
module EmptyAnalyzer

open FSharp.Analyzers.SDK

[<CliAnalyzer "EmptyAnalyzer">]
let empty: Analyzer<CliContext> = fun _ -> async { return [] }
"""

    analyzersDir

[<Test>]
let ``a script value that matches no file stops the run with code 20`` () =
    withFolder (fun folder ->
        let script = writeScript folder "let x = 1\n"
        let missing = Path.Combine(folder, "missing.fsx")

        let code, output =
            runCli
                [
                    "--script"
                    script
                    missing
                ]

        Assert.That(
            code &&& 0xFF,
            Is.EqualTo(exitCodeOf ExitErrorCodes.InvalidScriptArguments),
            output
        )

        Assert.That(output, Does.Contain "missing.fsx")
    )

[<Test>]
let ``fewer than one script at a time stops the run with code 20`` () =
    withFolder (fun folder ->
        let script = writeScript folder "let x = 1\n"

        let code, output =
            runCli
                [
                    "--script"
                    script
                    "--max-concurrent-scripts"
                    "0"
                ]

        Assert.That(
            code &&& 0xFF,
            Is.EqualTo(exitCodeOf ExitErrorCodes.InvalidScriptArguments),
            output
        )

        Assert.That(output, Does.Contain "--max-concurrent-scripts")
    )

let private checkDefineScript defines =
    withFolder (fun folder ->
        let script = writeScript folder "#if FOO\nlet x: int = \"not an int\"\n#endif\n"
        let analyzers = compileAnalyzer folder

        runCli (
            [
                "--analyzers-path"
                analyzers
                "--fail-on-type-errors"
                "--script"
                script
            ]
            @ defines
        )
    )

[<Test>]
let ``a defined symbol reaches the type check of the scripts`` () =
    let code, output =
        checkDefineScript
            [
                "--define"
                "FOO"
            ]

    Assert.That(code &&& 0xFF, Is.EqualTo(exitCodeOf ExitErrorCodes.TypeCheckFailed), output)

[<Test>]
let ``a symbol that is not defined leaves its code out of the type check`` () =
    let code, output = checkDefineScript []

    Assert.That(code &&& 0xFF, Is.EqualTo(exitCodeOf ExitErrorCodes.Success), output)

[<Test>]
let ``a define that is not one symbol stops the run with code 20`` () =
    withFolder (fun folder ->
        let script = writeScript folder "let x = 1\n"

        let code, output =
            runCli
                [
                    "--script"
                    script
                    "--define"
                    "A;B"
                ]

        Assert.That(
            code &&& 0xFF,
            Is.EqualTo(exitCodeOf ExitErrorCodes.InvalidScriptArguments),
            output
        )

        Assert.That(output, Does.Contain "A;B")
    )
