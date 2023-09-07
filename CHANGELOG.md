# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.13.0] - 2023-09-06

### Added
* [Add Parse and Check Results to Context](https://github.com/ionide/FSharp.Analyzers.SDK/pull/73) (thanks @dawedawe!)

### Changed
* [Enforce SDK version](https://github.com/ionide/FSharp.Analyzers.SDK/pull/72) (thanks @nojar!)
* [Update FCS to 43.7.400](https://github.com/ionide/FSharp.Analyzers.SDK/pull/74) (thanks @TheAngryByrd!)

## 0.12.0 - 2023-05-10

### Changed

* Updated the tool to .NET 7 (thanks @cmeeren!)
* Updated to FSharp.Core 7 and FCS 43.7.200 (aka what's in the 7.0.200 .NET SDK) (thanks @DamianReeves!)

## 0.11.0 - 2022-01-12

### Changed

* Update to FCS 41 - thanks @theangrybyrd!
* Pack the .NET Tool with RollForward set to enable running on .NET 6 runtimes.

## 0.10.1 - 2021-06-23

### Fixed

* Don't expose sourcelink as a nuget package dependency

## 0.10.0 - 2021-06-22

### Changed

* Update Ionide.ProjInfo to 0.53
* Update FCS to 40.0.0

## 0.9.0 - 2021-05-27

### Changed

* Exclude signature files from analysis in CLI
* Update Ionide.ProjInfo to 0.52

## 0.8.0 - 2021-02-10

### Changed

* Update FCS version to 39.0.0
* Update Ionide Project System version as well

## 0.7.0 - 2021-01-19

### Changed

* Update FCS version to 38.0.2
* Include MsBuild.Locator dependency

## 0.6.0 - 2020-12-20

### Changed

* Update FCS version to 38.0
* Use Ionide.ProjInfo isntead of Dotnet.ProjInfo

## 0.5.0 - 2020-07-11

### Changed

* Update FCS version to 36.0.3

## 0.4.1 - 2020-04-11

### Changed

* Update FCS version to 35.0.0

## 0.4.0 - 2020-03-08

### Added

* Allow for optional named analyzers via the attribute `[<Analyzer("AnalyzerName")>]`
* Add ability to get exact errors from running each individual analyzer

## 0.3.1 - 2020-02-28

### Changed

* Update FCS version to 34.1.0

## 0.3.0 - 2020-02-17

### Added

* Support third-party dependency resolution for analyzers

### Changed

* Manage analyzers state internally

## 0.2.0 - 2019-12-18

### Added

* Add `GetAllEntities` to context - function that returns all known entities in the workspace (from local projects and
  references)

## 0.1.0 - 2019-12-16

### Added

* Initial tool release

## 0.0.10 - 2019-11-21

### Added

* Add client module

## 0.0.9 - 2019-11-21

### Changed

* Update FCS version to 33.0.0

## 0.0.8 - 2019-10-01

### Changed

* Update FCS version to 31.0.0

## 0.0.7 - 2019-08-28

### Changed

* Update FCS version to 31.0.0

## 0.0.6 - 2019-07-01

### Changed

* Update FCS version to 30.0.0

## 0.0.5 - 2019-05-27

### Changed

* Update FCS version to 29.0

## 0.0.4 - 2019-03-29

### Changed

* Update FCS version

## 0.0.3 - 2019-02-26

### Changed

* Update FCS version

## 0.0.2 - 2019-02-09

### Changed

* Update FCS version

## 0.0.1 - 2018-09-14

### Added

* Initial release
