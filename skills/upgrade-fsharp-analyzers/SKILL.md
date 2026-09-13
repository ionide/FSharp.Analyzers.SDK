---
name: upgrade-fsharp-analyzers
description: Upgrade the fsharp-analyzers dotnet tool (FSharp.Analyzers.SDK) together with the analyzer packages a repo uses (Ionide.Analyzers, G-Research.FSharp.Analyzers, others). Resolves which SDK each analyzer release was built against, bumps versions consistently, re-runs the analyzers with the repo's own command and reports what changed, without touching source code. Use when asked to bump, update or upgrade F# analyzers, fsharp-analyzers, or the analyzers SDK.
---

# Upgrade F# analyzers

Upgrade the `fsharp-analyzers` tool and the analyzer packages in a repository to a consistent
set of versions, then show the user what the upgrade changed in the analysis run.

## The one rule that matters

The `fsharp-analyzers` tool only loads an analyzer assembly when that assembly was compiled
against the **same major.minor** of `FSharp.Analyzers.SDK` as the tool itself. On a mismatch
the tool logs

```
error: Trying to load <path>.dll which was built using SDK version 0.37.2.0. Expect 0.38.0.0 instead. Assembly will be skipped.
```

runs whatever did load, and then **exits with code 1** ("Because we failed to load some
assemblies to obtain analyzers from them, exiting"). There is no flag to tolerate a load
failure: `--exclude-analyzers` works on analyzer names, and the failure happens before any name
is known. So a stale package means a red CI step even on a clean codebase, unless the CI step
has `continue-on-error` or the stale `--analyzers-path` is removed.

Analyzer packages do **not** declare the SDK as a NuGet dependency. NuGet dependency metadata
and `dotnet outdated` cannot tell you which SDK a release targets. Step 2 explains how to find
out.

## Step 0: Guard rails

- This skill upgrades tooling. It does not fix analyzer findings. New or stricter analyzers
  after a bump are triaged per rule in step 6, and the user decides. Never edit `.fs` files
  unless the user explicitly asks after seeing the report.
- Do not commit or push. Leave the working tree for review. A scoped
  `git stash push <version files>` followed by `git stash pop`, used to re-run a baseline, is
  fine as long as the tree ends up back in the post-bump state.
- Report merge status and dates as facts. Never state or imply a release date the maintainers
  have not announced; "the bump is merged to main" is evidence, "days away" is a guess.
- Two scripts ship with this skill in `scripts/`. Refer to them by the path this SKILL.md was
  loaded from:
  - `sdk-from-source.sh <PackageId> [versions...]`: which SDK each release of an analyzer package
    was built against, read from the package's source repo at the published commit. Needs `gh`.
  - `sdk-version.fsx <dll>...`: the SDK version an analyzer DLL references and the analyzer names
    it registers. Needs `dotnet`. Works offline on restored packages.

## Step 1: Start from the release, then inventory the repo

### What shipped

The task is driven by a release event: a new SDK is out, what does it require and who has
caught up. Start with the tool, then come back to this loop with the analyzer package ids
once the inventory below has produced them:

```sh
for p in fsharp-analyzers <analyzer package ids from the inventory>; do
  v=$(curl -s "https://api.nuget.org/v3-flatcontainer/$p/index.json" | jq -r '.versions | map(select(test("-")|not)) | last')
  d=$(curl -s "https://api.nuget.org/v3/registration5-gz-semver2/$p/$v.json" | gunzip -c | jq -r '.published[:10]')
  printf '%s\t%s\t%s\n' "$p" "$v" "$d"
done
```

The tool `fsharp-analyzers` and the package `FSharp.Analyzers.SDK` always share a version
number, so the tool version is the SDK version. Read the SDK changelog
(`https://github.com/ionide/FSharp.Analyzers.SDK/blob/main/CHANGELOG.md`) for the range you are
about to cross and note breaking changes. Target framework bumps matter most (0.38.0 moved
everything to .NET 10): `global.json` and CI `setup-dotnet` steps may need a newer .NET before
anything runs.

### What the repo uses

Find analyzer packages by how they are wired, cheapest first:

1. `grep -rn 'IncludeAssets="analyzers"\|IncludeAssets>analyzers\|--analyzers-path' --include='*.props' --include='*.targets' --include='*.fsproj' --include='*.fsx' --include='*.sh' --include='*.yml' .`
   Every package referenced with analyzer assets, or whose path is passed to `--analyzers-path`,
   is an analyzer package. This normally answers the question with zero network calls.
2. Only for references that step 1 leaves ambiguous, check the nuspec tags for `fsharp-analyzer`:
   `curl -sL "https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.nuspec" | grep -o '<tags>[^<]*</tags>'`
   The tag is the publishing convention, and `https://azuresearch-usnc.nuget.org/query?q=tags:fsharp-analyzer`
   lists every published analyzer package. Use that to learn what exists, not to classify
   thirty references one by one.

Then record where each version lives; repos differ:

| What | Where |
| --- | --- |
| `fsharp-analyzers` tool | `.config/dotnet-tools.json` |
| Analyzer packages | `Directory.Packages.props`, `*.fsproj`, `Directory.Build.props`, `paket.dependencies` and `paket.lock` |
| `FSharp.Analyzers.Build` | same files. Optional MSBuild glue, many repos do not use it; skip if absent |
| `FSharp.Analyzers.SDK` package | only when the repo builds its own analyzers |
| Invocation flags | `FSharpAnalyzersOtherFlags` in `Directory.Build.targets`, build scripts, CI workflows |
| The CI step | `continue-on-error` on the analyzer step, and what consumes the results afterwards: a SARIF upload to code scanning, an artifact, or console only. This decides what a failure or a new finding costs |
| Lock files | `packages.lock.json`, `paket.lock`; skip if absent |
| .NET SDK pin | `global.json`, CI `setup-dotnet` |

Write down the flags too: `--treat-as-*`, `--exclude-analyzers`, `--include-analyzers` name
codes and analyzers that get renamed between releases (step 5 checks them).

## Step 2: Build the version matrix and choose a target

For each analyzer package, resolve which SDK its releases were built against. In order of
preference:

1. **The pin in the package's source repo at the published commit.** The nuspec records the
   repository URL and commit; the repo pins `FSharp.Analyzers.SDK` in `Directory.Packages.props`
   or an fsproj. Exact, no download, and it gives the whole history in one call:
   ```sh
   <skill>/scripts/sdk-from-source.sh Ionide.Analyzers 0.15.0 0.16.0 0.17.0
   <skill>/scripts/sdk-from-source.sh G-Research.FSharp.Analyzers      # all versions
   ```
   Falls back to `?` when the nuspec has no repository metadata.
2. **The assembly reference.** Authoritative and offline. After a restore the DLLs are in
   `~/.nuget/packages/<id>/<version>/analyzers/dotnet/fs/`; otherwise download the nupkg (a
   zip) and unzip it.
   ```sh
   dotnet fsi <skill>/scripts/sdk-version.fsx ~/.nuget/packages/ionide.analyzers/0.17.0/analyzers/dotnet/fs/*.dll
   ```
3. **Release notes and changelogs.** A cross-check only. Entries like
   `Update FSharp.Analyzers.SDK to 0.38.0` are the convention but not every release has one.

The recommendation rests on the "latest version" reading, so confirm it once:

```sh
curl -s "https://api.nuget.org/v3-flatcontainer/<id>/index.json" | jq -r '.versions[-5:][]'   # includes prereleases
gh api "repos/<owner>/<repo>/releases?per_page=3" --jq '.[] | "\(.tag_name) \(.published_at[:10])"'
gh api "repos/<owner>/<repo>/git/ref/tags/v<version>" --jq .object.sha                         # tag sha ...
curl -sL "https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.nuspec" | grep -o 'commit="[^"]*"'  # ... equals nuspec commit
```

The tag-equals-nuspec-commit check turns "a pin at some commit" into "the pin that shipped".
Not every repo tags releases (G-Research does not); the nuspec commit is still valid then.

Assemble the matrix. Publish dates belong in it, they change the recommendation:

| Package | Current | Candidate | Published | SDK it targets |
| --- | --- | --- | --- | --- |

Pick the target SDK:

- **Everything has a release for the latest SDK.** Target the latest. Done.
- **A package lags.** Find out whether the SDK bump is merged upstream or not started before
  recommending anything. Against its repo:
  ```sh
  # commits touching the pin, committer date (author dates arrive out of order on rebased repos)
  gh api "repos/<owner>/<repo>/commits?path=Directory.Packages.props" --jq '.[:5][] | "\(.sha[:8]) \(.commit.committer.date[:10]) \(.commit.message | split("\n")[0])"'
  # is the bump commit an ancestor of main?  status=ahead behind=0 means merged
  gh api "repos/<owner>/<repo>/compare/<bump-commit>...main" --jq '"\(.status) behind=\(.behind_by)"'
  ```
  Also read the `[Unreleased]` section of its changelog. "The bump is merged and waiting on a
  release" and "nobody has started" are different answers. Report those facts and the dates.
  Do not turn them into a predicted release date.

  Get the size of the coverage loss now, offline, not from a run:
  `dotnet fsi <skill>/scripts/sdk-version.fsx ~/.nuget/packages/<lagging-id>/<version>/analyzers/dotnet/fs/*.dll`
  prints the analyzer count.

  Then present four options with their consequences, using the CI facts from step 1, and let
  the user choose:
  1. **Upgrade what is ready, keep the lagging reference.** Its analyzers stop loading and the
     tool exits 1 on every run. With `continue-on-error` on the CI step that means coverage
     silently drops while PRs stay green; without it every PR goes red on a clean codebase.
  2. **Upgrade what is ready, remove the lagging package** (its reference and its
     `--analyzers-path`) until it ships. Exit 0 with an honestly reduced analyzer set. Note that
     the path may be computed in a build script rather than written as a literal, in which case
     removing it is a code edit there.
  3. **Stay on the newest SDK every package supports.** Often that is the current version,
     meaning no change today.
  4. **Wait** for the lagging release, with the merge status as evidence.

  Do not pick for the user. "Never drop a package" is a default, not a law.

Regardless of choice: `FSharp.Analyzers.Build`, if used, has no SDK coupling; take the latest.

## Step 3: Capture a baseline, once a bump is going to happen

Skip this step when step 2 ended in "no change today". Otherwise run the analyzers once with
the **current** versions so the after-run can be compared with it.

### Find the repo's own way of running analyzers

Every repository wires this up differently. Never construct a command; find the one the repo
already uses and run exactly that. It carries the flags, exclusions, project selection and
report locations that define what "the analyzers" means here. Look in this order:

1. **CI.** `grep -ril analy .github/workflows .gitlab-ci.yml azure-pipelines.yml 2>/dev/null`
   and read the matching step. Note `continue-on-error` on that step; it decides whether a
   load failure turns CI red.
2. **Build scripts.** `build.fsx`, `build.sh`, `Makefile`, `justfile`, `scripts/*.fsx`. CI
   usually calls one of these with a pipeline name (`dotnet fsi build.fsx -- -p Analyze`).
3. **MSBuild.** `FSharpAnalyzersOtherFlags` in `Directory.Build.targets` means
   `FSharp.Analyzers.Build` is in use: look for a solution target such as `AnalyzeSolution`,
   else the per-project `AnalyzeFSharpProject`. `RunAnalyzersDuringBuild=true` means they run
   inside `dotnet build`.
4. **Direct calls.**
   `grep -rn "fsharp-analyzers" --include='*.fsx' --include='*.sh' --include='*.yml' --include='*.json' .`

Note what shapes the run: excluded projects, changed-files-only modes (use the full-run form),
analyzer projects built in the repo (they must be rebuilt after the bump or the run uses stale
DLLs), prerequisites (`dotnet tool restore`, a prior build, `-c Release`), and where results
land (`--report` paths, a SARIF merge step, or console only).

If there is no wiring at all, the repo does not run analyzers today. Say so, and construct a
direct call only after the user confirms.

### Run it and record three things

Run the discovered command unchanged. Save the console output and any SARIF files to a scratch
directory outside the repo so the after-run cannot overwrite them. Do not add `--report` or
change flags to get nicer output. Console lines carry code, severity and location
(`File.fs(26,14): Warning IONIDE-005 : ...`). SARIF carries code, location and the rule's
`helpUri`; its `level` is omitted when it is the spec default, and that default is
`warning`, so a missing `level` means warning.

From the output, record:

1. The line `Registered N analyzers from M dlls`. This is the single clearest measure of what
   an upgrade costs in coverage, and the only place it is stated.
2. The exit code.
3. Findings per rule code (from SARIF `ruleId` or the console lines). Zero is a valid answer.

If the baseline already prints `which was built using SDK version`, say so before going on: the
repo is already running with fewer analyzers than it thinks, and that is not a regression of
this upgrade.

## Step 4: Apply the bump

Edit only version numbers: the tool in `dotnet-tools.json`, the package versions where step 1
found them, and `global.json` or CI dotnet setup when the target framework changed. Then
regenerate derived files rather than editing them:

```sh
dotnet tool restore
dotnet restore                   # refreshes packages.lock.json when the repo uses lock files
# paket: dotnet paket update --group <analyzers-group>
```

`dotnet restore --force-evaluate` if locked-mode restore rejects the change.

## Step 5: Run again and read the load phase first

Rebuild any in-repo analyzer projects, then run exactly the command from step 3. Before looking
at findings:

1. **Registered count.** Compare `Registered N analyzers from M dlls` with the baseline. A drop
   should correspond exactly to a package you knowingly left behind.
2. **Load errors.** If `which was built using SDK version` names a package you knowingly left
   behind, that is expected: report it as the cost of option 1 together with the exit code 1,
   and point at option 2 of step 2 (remove the package's reference and `--analyzers-path`,
   which may be a build script edit when the path is computed). If it names a package you
   upgraded, the matrix in step 2 was wrong for it; recheck with `sdk-version.fsx` on the
   restored DLL.
3. **Corroborate the count.** Run
   `dotnet fsi <skill>/scripts/sdk-version.fsx ~/.nuget/packages/<id>/<new-version>/analyzers/dotnet/fs/*.dll`
   for each package. The per-DLL counts should sum to the `Registered N` line, and each DLL
   should reference the new SDK.
4. **Stale flags.** Skip when step 1 recorded no `--exclude-analyzers`, `--include-analyzers`
   or `--treat-as-*` entries. Otherwise use the analyzer names the script just printed
   (`--verbosity d` does not list names or codes). Any name in an exclude or include flag that
   is not in the list is stale; the tool also logs `Excluding <Name> from <assembly>` only for
   names that matched, so a missing line is the same signal. For codes in `--treat-as-*`,
   compare against the codes documented by the analyzer package. Renames happen
   (`MixedPipeDirectionAnalyzer` became `MixedFlowDirectionAnalyzer` in Ionide.Analyzers 0.17.0).
5. **Confirm any unexplained delta.** If findings changed and no changelog entry accounts for
   it, re-run the baseline once before reporting: `git stash push <version files>`, run,
   `git stash pop`, run again. If the difference reproduces, it is real. Note that an SDK bump
   also moves the F# compiler service underneath every analyzer, so new hits can appear
   without any analyzer changing. Real example: SDK 0.38.0 moved to FCS 43.12.400, where
   `match s with "" ->` is exposed in the typed tree as `s = ""`; the previous compiler exposed
   it as `s.Length = 0`, so Ionide's EmptyStringAnalyzer (IONIDE-005) started matching code it
   had walked past before, with no analyzer change at all (dotnet/fsharp#19923). Such a hit is
   a real effect of the upgrade; name the cause, and report upstream only when it looks wrong.

## Step 6: Report, then stop

Compare after with baseline by rule code and report:

1. Versions changed: package, old, new, published, SDK it targets. Files touched.
2. Breaking changes from the changelogs that affected this repo and what was done
   (`global.json`, CI).
3. Coverage: registered analyzers and exit code, before and after. If coverage dropped, name
   the package, how many analyzers, and what CI does with the exit code. State the upstream
   merge status; do not predict a release date.
4. Where the results go. A new warning that cannot fail the build may still become a code
   scanning alert through a SARIF upload step; say so when step 1 found one.
5. Findings that disappeared, grouped by code.
6. New findings, grouped by code: count, severity (from the console line), one example
   location, and the help link from the SARIF rule metadata:
   `jq -r '.runs[].tool.driver.rules[]? | select(.id=="CODE") | .helpUri' report.sarif`
   (it lives on the rule, not on the result). For one or two findings, plain sentences beat a
   table. For each code, present the options and recommend one with a one-line reason:
   - **Fix the code.** Small counts or rules that catch real bugs.
   - **Downgrade severity** with `--treat-as-warning CODE` / `--treat-as-info` / `--treat-as-hint`
     so CI stays green while the team works through them.
   - **Disable the analyzer** with `--exclude-analyzers <AnalyzerName>` when the rule does not
     fit the codebase.
   - **Suppress specific hits** in source with `// fsharpanalyzer: ignore-line CODE`
     (`ignore-line-next`, `ignore-region-start` / `ignore-region-end`, `ignore-file`). Source
     edits, only on request.
   - **Flag it as unexplained and report upstream** when the finding is not predicted by any
     changelog entry and survived the re-baseline in step 5. Say what changed (tool, SDK,
     compiler service, analyzer package) and link the analyzer's issue tracker. Do not triage it
     as intended behaviour.
7. Stale flag entries from step 5.
8. Anything that still needs the user.

A clean run, zero findings before and zero after, is a complete result: report the versions,
the coverage table and "no findings before or after", and do not manufacture sections 5 and 6.

Then wait. Only after the user chooses do you edit flags or source. The intended end state is a
working tree with version bumps, regenerated lock files, maybe a `global.json` change, and this
report. fsprojects/fantomas#3046 is a reference for what that change looks like.
