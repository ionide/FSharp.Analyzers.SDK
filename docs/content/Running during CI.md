---
category: end-users
categoryindex: 1
index: 6
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

### GitHub Actions

If you are using [GitHub Actions](https://docs.github.com/en/code-security/codeql-cli/using-the-advanced-functionality-of-the-codeql-cli/sarif-output) you can easily send the *sarif file* to [CodeQL](https://codeql.github.com/).

```yml
    # checkout code, build, run analyzers, ...
    - name: Upload SARIF file
      uses: github/codeql-action/upload-sarif@v2
      with:
        sarif_file: analysis.sarif
```

You might need to give workflows in your repository the `Read and write permissions` for the sarif upload to succeed.  
Go to `Settings` -> `Actions` -> `General` and check the `Workflow permissions` section.

Sample:

![Example](https://user-images.githubusercontent.com/2621499/275484611-e38461f8-3689-4bf0-8ab8-11a6318e01aa.png)

See [fsproject/fantomas#2962](https://github.com/fsprojects/fantomas/pull/2962) for more information.

[Previous]({{fsdocs-previous-page-link}})
