#r "nuget: Fun.Build, 0.5.2"

open Fun.Build

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
    stage "test" { run "dotnet test -c Release --no-build" }
    stage "sample" {
        run
            "dotnet run --project src/FSharp.Analyzers.Cli/FSharp.Analyzers.Cli.fsproj -- --project ./samples/OptionAnalyzer/OptionAnalyzer.fsproj --analyzers-path ./samples/OptionAnalyzer/bin/Release --verbose"
    }
    stage "docs" {
        run "dotnet fsdocs build --properties Configuration=Release --eval --nodefaultcontent --clean --strict"
    }
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
