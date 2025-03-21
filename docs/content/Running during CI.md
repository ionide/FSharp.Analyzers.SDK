---
category: end-users
categoryindex: 2
index: 5
---

# Running analyzers during continuous integration

Similar to unit tests and code formatting, analyzers are a tool you want to enforce when modifying a code repository.  
Especially, in the context of a team, you want to ensure everybody is adhering to the warnings produced by analyzers.

## Command line options

Use the `--report` command line argument to produce a [sarif](https://sarifweb.azurewebsites.net/) report json.  
Most *CI/CD* systems should be able to process this afterwards to capture the reported information by the analyzers.

Example usage:

```shell
dotnet fsharp-analyzers /
  --project MyProject.fsproj /
  --analyzers-path ./MyFolderWithAnalyzers /
  --report ./analysis.sarif
```

### Code root

Use the `--code-root` flag to specify the root directory where all reported problems should be relative to.
Typically, this should correspond to your source control (git) repository. Some tooling may require this setting to be accurate for easy navigation to the reported problems.

Example when using MSBuild:

```xml
<PropertyGroup>
    <CodeRoot>$([System.IO.Path]::GetDirectoryName($(DirectoryBuildTargetsPath)))</CodeRoot>
    <SarifOutput>$(CodeRoot)/reports/</SarifOutput>
    <FSharpAnalyzersOtherFlags>--analyzers-path &quot;$(PkgG-Research_FSharp_Analyzers)/analyzers/dotnet/fs&quot;</FSharpAnalyzersOtherFlags>
    <FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --code-root &quot;$(CodeRoot)&quot;</FSharpAnalyzersOtherFlags>
    <FSharpAnalyzersOtherFlags>$(FSharpAnalyzersOtherFlags) --report &quot;$(SarifOutput)$(MSBuildProjectName)-$(TargetFramework).sarif&quot;</FSharpAnalyzersOtherFlags>
</PropertyGroup>
```

## GitHub Actions

### GitHub Advanced Security
If you are using [GitHub Actions](https://docs.github.com/en/code-security/codeql-cli/using-the-advanced-functionality-of-the-codeql-cli/sarif-output) you can easily send the *sarif file* to [CodeQL](https://codeql.github.com/).

```yml
    - name: Run Analyzers
      run: dotnet msbuild /t:AnalyzeFSharpProject /p:Configuration=Release
      # This is important, you want to continue your Action even if you found problems.
      # As you always want the report to upload
      continue-on-error: true

    # checkout code, build, run analyzers, ...
    - name: Upload SARIF file
      uses: github/codeql-action/upload-sarif@v2
      with:
        # You can also specify the path to a folder for `sarif_file`
        sarif_file: analysis.sarif
```

You might need to give workflows in your repository the `Read and write permissions` for the sarif upload to succeed.  
Go to `Settings` -> `Actions` -> `General` and check the `Workflow permissions` section.

Sample:

![Example](https://user-images.githubusercontent.com/2621499/275484611-e38461f8-3689-4bf0-8ab8-11a6318e01aa.png)

See [fsproject/fantomas#2962](https://github.com/fsprojects/fantomas/pull/2962) for more information.

### Github Workflow Commands
If you cannot use GitHub Advanced Security (e.g. if your repository is private), you can get similar annotations by running the analyzers with `--output-format github`.
This will make the analyzers print their results as [GitHub Workflow Commands](https://docs.github.com/en/actions/writing-workflows/choosing-what-your-workflow-does/workflow-commands-for-github-actions).
If you for instance have a GitHub Action to run analyzers on every pull request, these annotations will show up in the "Files changed" on the pull request.
If the annotations don't show correctly, you might need to set the `code-root` to the root of the repository.

Note that GitHub has a hard limit of 10 annotations of each type (notice, warning, error) per CI step.
This means that only the first 10 errors, the first 10 warnings and the first 10 hints/info results from analyzers will generate annotations.
The workflow log will contain all analyzer results even if a job hits the annotation limits.

[Previous]({{fsdocs-previous-page-link}})
