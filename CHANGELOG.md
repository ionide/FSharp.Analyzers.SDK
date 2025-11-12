# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.34.1] - 2025-11-11

### Changed

- [Adds various helpers for FSharpSnapshot and multi-file projects](https://github.com/ionide/FSharp.Analyzers.SDK/pull/261) (thanks @TheAngryByrd!)
- [Add RSP file support for FSC arguments to bypass CLI length limits](https://github.com/ionide/FSharp.Analyzers.SDK/pull/268) (thanks @Copilot!)
- [Update McMaster.NETCore.Plugins to v2](https://github.com/ionide/FSharp.Analyzers.SDK/pull/233) (thanks @Numpsy!)
- [Update FSharp.Compiler.Service 43.10.100](https://github.com/ionide/FSharp.Analyzers.SDK/pull/270/files) (thanks @Numpsy!)

## [0.33.1] - 2025-10-19

### Fixed 

- [Expose getAnalyzerIgnoreRanges function for public consumption](https://github.com/ionide/FSharp.Analyzers.SDK/pull/258) (thanks @TheAngryByrd!)

## [0.33.0] - 2025-10-19

### Added

- Support for Analyzers using NuGet in FsAutocomplete [253](https://github.com/ionide/FSharp.Analyzers.SDK/pull/253) [255](https://github.com/ionide/FSharp.Analyzers.SDK/pull/255) (thanks @TheAngryByrd!)
- [Adds CLI Flag to get binlogs for project loads. Configures ProjInfo logging.](https://github.com/ionide/FSharp.Analyzers.SDK/pull/256) (thanks @TheAngryByrd!)
- [Add Ignore Functionality for Analyzers](https://github.com/ionide/FSharp.Analyzers.SDK/pull/254) (thanks @1eyewonder!)

### Changed

- [Update Ionide.KeepAChangelog.Tasks to 0.3.0](https://github.com/ionide/FSharp.Analyzers.SDK/pull/244) (thanks @Numpsy!)
- [Add EditorContext to OptionAnalyzer sample](https://github.com/ionide/FSharp.Analyzers.SDK/pull/249) (thanks @TheAngryByrd!)
- [Add logging about System.Runtime load exceptions](https://github.com/ionide/FSharp.Analyzers.SDK/pull/250) (thanks @TheAngryByrd!)
- [Fix the path to the OptionsAnalyzer binary in the readme](https://github.com/ionide/FSharp.Analyzers.SDK/pull/252/) (thanks @Numpsy!)


## [0.32.1] - 2025-08-02

### Changed 

- [Perf: Move membersToIgnore/exprTypesToIgnore out of visitDeclaration()](https://github.com/ionide/FSharp.Analyzers.SDK/pull/238) (thanks @Numpsy!)

## [0.32.0] - 2025-07-12

### Added

- [Support script (fsx) files](https://github.com/ionide/FSharp.Analyzers.SDK/pull/237) (thanks @TheAngryByrd!)

### Changed

- [Add project options to analyzer contexts](https://github.com/ionide/FSharp.Analyzers.SDK/pull/236) (Thanks @Numpsy!)

## [0.31.0] - 2025-05-17

### Changed

- [Update FSharp.Compiler.Service to 43.9.300](https://github.com/ionide/FSharp.Analyzers.SDK/pull/232) (Thanks @Numpsy!)
- [Update Ionide.ProjInfo.ProjectSystem package version to 0.71.0](https://github.com/ionide/FSharp.Analyzers.SDK/pull/235) (Thanks @TheAngryByrd!)

## [0.30.0] - 2025-03-25

### Added

- [Add GitHub logging output format](https://github.com/ionide/FSharp.Analyzers.SDK/pull/230) (Thanks @fkj!)

## [0.29.1] - 2025-03-06

### Fixed

- [Prevent File Locking Issue](https://github.com/ionide/FSharp.Analyzers.SDK/pull/228) (Thanks @1eyewonder!)

## [0.29.0] - 2025-02-12

### Changed

- [Update FSharp.Compiler.Service to 43.9.201](https://github.com/ionide/FSharp.Analyzers.SDK/pull/226) (Thanks @TheAngryByrd!)

## [0.28.0] - 2024-11-19

### Added

- [Support for F# 9 and .NET 9.0.100](https://github.com/ionide/FSharp.Analyzers.SDK/pull/222) (Thanks @Numpsy!)

### Removed

- [Support for .NET 6 and .NET 7](https://github.com/ionide/FSharp.Analyzers.SDK/pull/222) (Thanks @Numpsy!)

## [0.27.0] - 2024-08-17

### Changed

- [Update FCS and FSharp.Core to the .NET SDK 8.0.400 release versions](https://github.com/ionide/FSharp.Analyzers.SDK/pull/218)


## [0.26.1] - 2024-08-05

### Fixed
- [Reset the console foreground colour after printing results](https://github.com/ionide/FSharp.Analyzers.SDK/pull/216) (thanks @Numpsy!)
- [Only Analyze projects passed in via CLI](https://github.com/ionide/FSharp.Analyzers.SDK/pull/217) (thanks @TheAngryByrd)

## [0.26.0] - 2024-05-15

### Changed

- [Emit Analyzer Errors in the MSBuild Canonical Error Format](https://github.com/ionide/FSharp.Analyzers.SDK/pull/208) (thanks @Numpsy!)
- [Update Structured Logger libraries](https://github.com/ionide/FSharp.Analyzers.SDK/pull/211) (thanks @nojaf!)
- Update FSharp.Compiler.Service and FSharp.Core to the .NET SDK 8.0.300 release versions
- [More efficiently analyze larger solutions](https://github.com/ionide/FSharp.Analyzers.SDK/pull/210) (thanks @TheAngryByrd!)

## [0.25.0] - 2024-02-14

### Changed

- [Update to FCS and FSharp.Core 43.8.200](https://github.com/ionide/FSharp.Analyzers.SDK/pull/207)
- [Fail when no analyzers are registered but the tool is invoked](https://github.com/ionide/FSharp.Analyzers.SDK/pull/202) (thanks @Smaug123!)
- [Remove Coverlet.Collector package dependency](https://github.com/ionide/FSharp.Analyzers.SDK/pull/206) (thanks @dawedawe!)
- [Update Ionide.ProjInfo to 0.63.0](https://github.com/ionide/FSharp.Analyzers.SDK/pull/205) (thanks @nojaf!)

## [0.24.0] - 2024-01-30

### Changed
- [Add `RequireQualifiedAccess` to `Severity` type](https://github.com/ionide/FSharp.Analyzers.SDK/pull/199) (thanks @Smaug123!)
- [Fail the tool when any analyzers fail to load](https://github.com/ionide/FSharp.Analyzers.SDK/pull/198) (thanks @Smaug123!)

## [0.23.0] - 2024-01-05

### Changed
- [Changed --exclude-analyzer to --exclude-analyzers](https://github.com/ionide/FSharp.Analyzers.SDK/pull/196) (thanks @dawedawe!)
- [Changed --ignore-files to --exclude-files](https://github.com/ionide/FSharp.Analyzers.SDK/pull/196) (thanks @dawedawe!)

### Added
- [Add missing TAST walkers](https://github.com/ionide/FSharp.Analyzers.SDK/pull/185) (thanks @dawedawe!)
- [Add support for --include-analyzers to ignore all others](https://github.com/ionide/FSharp.Analyzers.SDK/pull/194) (thanks @dawedawe!)
- [Add support for --include-files to ignore all others](https://github.com/ionide/FSharp.Analyzers.SDK/pull/196) (thanks @dawedawe!)

## [0.22.0] - 2023-12-19

### Changed
- [Add path to ASTCollecting](https://github.com/ionide/FSharp.Analyzers.SDK/pull/171) (thanks @nojaf!)
- [Use Microsoft.Extensions.Logging instead of printf based logging infrastructure](https://github.com/ionide/FSharp.Analyzers.SDK/pull/175) (thanks @dawedawe!)

## [0.21.0] - 2023-11-22

### Changed
- [Update FCS to 43.8.100](https://github.com/ionide/FSharp.Analyzers.SDK/pull/168) (thanks @nojaf!)

## [0.20.2] - 2023-11-14

### Fixed
- [Raise clear error if empty FSC arguments were passed](https://github.com/ionide/FSharp.Analyzers.SDK/pull/162) (thanks @nojaf!)

## [0.20.1] - 2023-11-14

### Fixed
- [Extend signature AST walking](https://github.com/ionide/FSharp.Analyzers.SDK/pull/161) (thanks @nojaf!)

## [0.20.0] - 2023-11-13

### Fixed
- [--project value should be tested if path exists](https://github.com/ionide/FSharp.Analyzers.SDK/issues/141) (thanks @dawedawe!)
- [Provide better DX when project cracking failed](https://github.com/ionide/FSharp.Analyzers.SDK/issues/126) (thanks @dawedawe!)
- [Hint is mapped as note in sarif export](https://github.com/ionide/FSharp.Analyzers.SDK/pull/148) (thanks @nojaf!)
- [Properly walk SynModuleSigDecl.Val](https://github.com/ionide/FSharp.Analyzers.SDK/pull/156) (thanks @nojaf!)
- [Sarif file should not report absolute file paths](https://github.com/ionide/FSharp.Analyzers.SDK/issues/154) (thanks @nojaf!)

### Added
- [Add code-root flag](https://github.com/ionide/FSharp.Analyzers.SDK/pull/157) (thanks @nojaf!)
- [Add a -p flag to allow passing MSBuild properties through to the MSBuild evaluation portion of checking.](https://github.com/ionide/FSharp.Analyzers.SDK/issues/84) (thanks @dawedawe!)

## [0.19.0] - 2023-11-08

### Changed
- [API change in TASTCollecting](https://github.com/ionide/FSharp.Analyzers.SDK/pull/145) (thanks @dawedawe!)

### Fixed
- [Walk over the union case in SynModuleDecl.Exception](https://github.com/ionide/FSharp.Analyzers.SDK/pull/147) (thanks @nojaf!)

## [0.18.0] - 2023-11-03

### Added
- [Allow remapping of all severity levels](https://github.com/ionide/FSharp.Analyzers.SDK/pull/138) (thanks @dawedawe!)
- [Add tree processing infrastructure](https://github.com/ionide/FSharp.Analyzers.SDK/pull/140) (thanks @dawedawe!)

## [0.17.1] - 2023-10-30

### Fixed
- [Create sarif folder if it does not exist](https://github.com/ionide/FSharp.Analyzers.SDK/issues/132) (thanks @nojaf!)

## [0.17.0] - 2023-10-26

### Changed
- [Use fixed version of FCS and FSharp.Core](https://github.com/ionide/FSharp.Analyzers.SDK/pull/127) (thanks @nojaf!)
- [Allow to specify multiple analyzers-paths](https://github.com/ionide/FSharp.Analyzers.SDK/pull/128) (thanks @nojaf!)

### Added
- [Accept direct fsc arguments as input](https://github.com/ionide/FSharp.Analyzers.SDK/pull/129) (thanks @nojaf!)

## [0.16.0] - 2023-10-16

### Added
- [Analyzer report ](https://github.com/ionide/FSharp.Analyzers.SDK/issues/110) (thanks @nojaf!)

## [0.15.0] - 2023-10-10

### Added
- [Support multiple project parameters in the Cli tool](https://github.com/ionide/FSharp.Analyzers.SDK/pull/116) (thanks @dawedawe!)
- [Exclude analyzers](https://github.com/ionide/FSharp.Analyzers.SDK/issues/112) (thanks @nojaf!)

## [0.14.1] - 2023-09-26

### Changed
- [Removed repo internal packages.lock.json files to workaround sdk bug](https://github.com/ionide/FSharp.Analyzers.SDK/pull/107) (thanks @dawedawe!)

## [0.14.0] - 2023-09-21

### Added
- [`FSharp.Analyzers.SDK.Testing` NuGet package](https://github.com/ionide/FSharp.Analyzers.SDK/pull/88) (thanks @dawedawe!)

### Changed
- [Revisit Context type](https://github.com/ionide/FSharp.Analyzers.SDK/pull/90) (thanks @nojaf!)
- [Don't filter out .fsi files](https://github.com/ionide/FSharp.Analyzers.SDK/pull/83) (thanks @dawedawe!)
- [update to projinfo 0.62 to fix runtime failure](https://github.com/ionide/FSharp.Analyzers.SDK/pull/77) (thanks @dawedawe!)

## [0.13.0] - 2023-09-06

### Added
- [Add Parse and Check Results to Context](https://github.com/ionide/FSharp.Analyzers.SDK/pull/73) (thanks @dawedawe!)

### Changed
- [Enforce SDK version](https://github.com/ionide/FSharp.Analyzers.SDK/pull/72) (thanks @nojaf!)
- [Update FCS to 43.7.400](https://github.com/ionide/FSharp.Analyzers.SDK/pull/74) (thanks @TheAngryByrd!)

## [0.12.0] - 2023-05-10

### Changed

- Updated the tool to .NET 7 (thanks @cmeeren!)
- Updated to FSharp.Core 7 and FCS 43.7.200 (aka what's in the 7.0.200 .NET SDK) (thanks @DamianReeves!)

## [0.11.0] - 2022-01-12

### Changed

- Update to FCS 41 - thanks @theangrybyrd!
- Pack the .NET Tool with RollForward set to enable running on .NET 6 runtimes.

## [0.10.1] - 2021-06-23

### Fixed

- Don't expose sourcelink as a nuget package dependency

## [0.10.0] - 2021-06-22

### Changed

- Update Ionide.ProjInfo to 0.53
- Update FCS to 40.0.0

## [0.9.0] - 2021-05-27

### Changed

- Exclude signature files from analysis in CLI
- Update Ionide.ProjInfo to 0.52

## [0.8.0] - 2021-02-10

### Changed

- Update FCS version to 39.0.0
- Update Ionide Project System version as well

## [0.7.0] - 2021-01-19

### Changed

- Update FCS version to 38.0.2
- Include MsBuild.Locator dependency

## [0.6.0] - 2020-12-20

### Changed

- Update FCS version to 38.0
- Use Ionide.ProjInfo isntead of Dotnet.ProjInfo

## [0.5.0] - 2020-07-11

### Changed

- Update FCS version to 36.0.3

## [0.4.1] - 2020-04-11

### Changed

- Update FCS version to 35.0.0

## [0.4.0] - 2020-03-08

### Added

- Allow for optional named analyzers via the attribute `[<Analyzer("AnalyzerName")>]`
- Add ability to get exact errors from running each individual analyzer

## [0.3.1] - 2020-02-28

### Changed

- Update FCS version to 34.1.0

## [0.3.0] - 2020-02-17

### Added

- Support third-party dependency resolution for analyzers

### Changed

- Manage analyzers state internally

## [0.2.0] - 2019-12-18

### Added

- Add `GetAllEntities` to context - function that returns all known entities in the workspace (from local projects and references)

## [0.1.0] - 2019-12-16

### Added

- Initial tool release

## [0.0.10] - 2019-11-21

### Added

- Add client module

## [0.0.9] - 2019-11-21

### Changed

- Update FCS version to 33.0.0

## [0.0.8] - 2019-10-01

### Changed

- Update FCS version to 31.0.0

## [0.0.7] - 2019-08-28

### Changed

- Update FCS version to 31.0.0

## [0.0.6] - 2019-07-01

### Changed

- Update FCS version to 30.0.0

## [0.0.5] - 2019-05-27

### Changed

- Update FCS version to 29.0

## [0.0.4] - 2019-03-29

### Changed

- Update FCS version

## [0.0.3] - 2019-02-26

### Changed

- Update FCS version

## [0.0.2] - 2019-02-09

### Changed

- Update FCS version

## [0.0.1] - 2018-09-14

### Added

- Initial release
