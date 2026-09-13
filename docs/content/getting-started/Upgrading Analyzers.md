---
category: getting-started
categoryindex: 1
index: 6
---

# Upgrading Analyzers

The `fsharp-analyzers` tool only loads analyzer assemblies that were compiled against the same major.minor version of `FSharp.Analyzers.SDK` as the tool itself.
When a new SDK ships, every analyzer package you use has to move to a release built against it, or the tool skips that assembly and exits with a non-zero code.
Analyzer packages do not declare the SDK as a NuGet dependency, so `dotnet outdated` and similar tools cannot tell you which versions belong together.

## Upgrading with an agent skill

This repository ships an [agent skill](https://github.com/ionide/FSharp.Analyzers.SDK/tree/main/skills/upgrade-fsharp-analyzers) that does the bookkeeping for you.
Install it once, globally, with the [skills CLI](https://skills.sh):

```shell
npx skills add ionide/FSharp.Analyzers.SDK --skill upgrade-fsharp-analyzers -g
```

Then ask your agent to upgrade the F# analyzers in a repository that uses them. The skill will:

1. Find the tool and every analyzer package the repository references, and where each version lives.
2. Resolve which SDK each analyzer release was built against, by reading the pin in the analyzer's own source repository at the published commit, with the assembly reference as a fallback.
3. Pick a target SDK version. When a package has no compatible release yet, it presents the options and lets you decide rather than dropping the package silently.
4. Run the analyzers with the repository's own command before and after the bump, and compare the number of registered analyzers, the exit code and the findings per rule.
5. Report what changed. It does not modify source code; new findings are triaged per rule by you.

## Doing it by hand

If you prefer to do this manually, the essentials are:

- Bump `fsharp-analyzers` in `dotnet-tools.json` and every analyzer package to a release built against the same SDK. The tool version and the `FSharp.Analyzers.SDK` package version are always the same number.
- Read the changelog of each analyzer package for a line like `Update FSharp.Analyzers.SDK to 0.38.0`.
- Run the analyzers and check the log for `which was built using SDK version`. That line means a package is still on the old SDK.
- Expect the occasional new finding even when no analyzer changed. Each SDK release also updates the F# compiler service, and the typed tree the analyzers walk can change shape between compiler versions.

[Previous]({{fsdocs-previous-page-link}})
