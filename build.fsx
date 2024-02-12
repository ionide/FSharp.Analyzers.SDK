#r "nuget: Fun.Build, 0.5.2"

open Fun.Build
open System.IO

let purgeBinLogCache () =
    let binLogCache =
        Path.Combine(Path.GetTempPath(), "FSharp.Analyzers.SDK.BinLogCache")
    if (Directory.Exists(binLogCache)) then
        Directory.Delete(binLogCache, true)

let restoreStage =
    stage "restore" {
        run "dotnet tool restore"
        run "dotnet restore --locked-mode"
    }

let buildStage =
    stage "build" { run "dotnet build -c Release --no-restore -maxCpuCount" }

pipeline "Build" {
    restoreStage
    stage "lint" { run "dotnet fantomas . --check" }
    stage "build" { run "dotnet build -c Release --no-restore -maxCpuCount" }
    stage "test" {
        purgeBinLogCache ()
        run "dotnet test -c Release --no-build"
    }
    stage "sample" {
        run
            "dotnet run --project src/FSharp.Analyzers.Cli/FSharp.Analyzers.Cli.fsproj -- --project ./samples/OptionAnalyzer/OptionAnalyzer.fsproj --analyzers-path ./artifacts/bin/OptionAnalyzer/release --verbosity d"
    }
    stage "docs" { run "dotnet fsdocs build --properties Configuration=Release --eval --clean --strict" }
    runIfOnlySpecified false
}

pipeline "ReleaseBuild" {
    restoreStage
    buildStage
    runIfOnlySpecified true
}

pipeline "Docs" {
    restoreStage
    buildStage
    stage "fsdocs" { run "dotnet fsdocs watch --properties Configuration=Release --eval" }
    runIfOnlySpecified true
}

tryPrintPipelineCommandHelp ()
