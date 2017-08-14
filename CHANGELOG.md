# Changelog
All changes to the project will be documented in this file.

## [1.23.2] - 2017-08-14

* Set CscToolExe to 'csc.exe' to address issues with older Mono installations where the MSBuild targets have set it to 'mcs.exe'.

## [1.23.1] - 2017-08-08

* Fixed two regressions with MSBuild projects:
  1. .NET Core projects were not properly processed if Mono was installed.
  2. When Mono is installed, don't set `MSBuildExtensionsPath` to `$mono_prefix/xbuild` unless both `$mono_prefix/msbuild` and `$mono_prefix/xbuild/15.0` also exist.
* Properly set new language version values to support C# 7.1.

## [1.23.0] - 2017-08-07

Note: This release now requires the latest release of Mono 5.2.0 or later to build and run. In addition, there are now six flavors built for every release:

* Windows builds that run on Desktop CLR.
    * omnisharp-win-x86.zip
    * omnisharp-win-x64.zip
* A *Nix build that be run on Mono 5.2.0 or greater. (Note that the `--assembly-loader=strict` flag must be specified when launch this build with Mono).
    * omnisharp-mono.tar.gz
* Standalone builds for OSX and Linux that include the Mono bits necessary to run OmniSharp.
    * omnisharp-osx.tar.gz
    * omnisharp-linux-x86.tar.gz
    * omnisharp-linux-x64.tar.gz

#### Detailed Changes

* Updated detection of Mono path to p/invoke into `real_path` in `libc` to properly resolve symlinks. (PR: [#911](https://github.com/OmniSharp/omnisharp-roslyn/pull/911))
* Fixed a Script project system regression introduced as part of [#898](https://github.com/OmniSharp/omnisharp-roslyn/pull/898), that caused CSX support to break for Desktop CLR scripts on Windows (PR: [#913](https://github.com/OmniSharp/omnisharp-roslyn/pull/913))
* Set `DOTNET_UI_LANGUAGE` environment variable while running `dotnet --info` to ensure that the output is not localized. (PR: [#914](https://github.com/OmniSharp/omnisharp-roslyn/pull/914))
* OmniSharp now targets net46 by default. ([#666](https://github.com/OmniSharp/omnisharp-roslyn/pull/666), PR: ([#915](https://github.com/OmniSharp/omnisharp-roslyn/pull/915)))
* Fixed typo in help output. (PR: [#916](https://github.com/OmniSharp/omnisharp-roslyn/pull/916))
* xUnit updated to latest 2.3.0 nightly beta, fixing running of xUnit tests inside VS 2017. (PR: [#917](https://github.com/OmniSharp/omnisharp-roslyn/pull/917))
* Fix solution parsing (again!) by introducing custom solution parsing API. ([omnisharp-vscode#1645](https://github.com/OmniSharp/omnisharp-vscode/issues/1645), PR: [#918](https://github.com/OmniSharp/omnisharp-roslyn/pull/918))
* Globally set various MSBuild properties to better support Mono-based projects. ([#892](https://github.com/OmniSharp/omnisharp-roslyn/issues/892), [omnisharp-vscode#1597](https://github.com/OmniSharp/omnisharp-vscode/issues/1597), [omnisharp-vscode#1624](https://github.com/OmniSharp/omnisharp-vscode/issues/1624), [omnisharp-vscode#1396](https://github.com/OmniSharp/omnisharp-vscode/issues/1396), PR: [#923](https://github.com/OmniSharp/omnisharp-roslyn/pull/923))
* Big changes to the build which improve build performance and move OmniSharp to Mono 5.2.0. (PR: [#924](https://github.com/OmniSharp/omnisharp-roslyn/pull/924))
* Update to Roslyn 2.3.0 packages. (PRs: [#930](https://github.com/OmniSharp/omnisharp-roslyn/pull/930), [#931](https://github.com/OmniSharp/omnisharp-roslyn/pull/931))

## [1.22.0] - 2017-07-07

* Allow go to definition to work from metadata as source. ([#876](https://github.com/OmniSharp/omnisharp-roslyn/issues/876), PR: [#883](https://github.com/OmniSharp/omnisharp-roslyn/pull/883))
* Support added for referencing NuGet packages in C# scripts. (PR: [#813](https://github.com/OmniSharp/omnisharp-roslyn/pull/813))
* Use MSBuild solution parsing API which is the official parser for handling weird solution file cases. ([omnisharp-vscode#1580](https://github.com/OmniSharp/omnisharp-vscode/issues/1580), PR: [#897](https://github.com/OmniSharp/omnisharp-roslyn/pull/897))
* Improvements to logic that computes code fixes and refactorings. (PR: [#877](https://github.com/OmniSharp/omnisharp-roslyn/pull/899))
* Update to Roslyn 2.3.0-beta2, which brings support for C# 7.1. (PRs: [#900](https://github.com/OmniSharp/omnisharp-roslyn/pull/900) and [#901](https://github.com/OmniSharp/omnisharp-roslyn/pull/901))
* Ensure that all project systems support an "Enabled" property that can be configured in omnisharp.json. (PR: [#902](https://github.com/OmniSharp/omnisharp-roslyn/pull/902))
* Change MSBuild project system to call the "Compile" target rather than the "ResolveReferences" target, allowing targets that generate files to run. ([omnisharp-vscode#1531](https://github.com/OmniSharp/omnisharp-vscode/issues/1531))
* Update MSBuild to latest version ([#904](https://github.com/OmniSharp/omnisharp-roslyn/pull/904), PR: [#907](https://github.com/OmniSharp/omnisharp-roslyn/pull/907))
* Added binding redirects for MSBuild, fixing issues with custom MSBuild tasks built with different versions of MSBuild. ([#903](https://github.com/OmniSharp/omnisharp-roslyn/issues/903))
* System.dll is now added correctly for C# scripts targeting .NET Framework ([omnisharp-vscode#1581](https://github.com/OmniSharp/omnisharp-vscode/issues/1581), PR: [#898](https://github.com/OmniSharp/omnisharp-roslyn/pull/898))

## [1.21.0] - 2017-06-07

* Moved back to NuGet 4.0.0 RTM packages. This will help alleviate problems with using OmniSharp with .NET Core 2.0-preview2 builds ([#865](https://github.com/OmniSharp/omnisharp-roslyn/issues/865), PR: [#885](https://github.com/OmniSharp/omnisharp-roslyn/pull/885)).

## [1.20.0] - 2017-06-02

* **Breaking Change**: When using environment variables to configure OmniSharp, they must be prefixed by 'OMNISHARP_', which helps ensure that OmniSharp will not unintentionally consume other environment variables (such as 'msbuild') if they happen to be set. See [Configuration Options](https://github.com/OmniSharp/omnisharp-roslyn/wiki/Configuration-Options) for more details on configuring OmniSharp. ([omnisharp-vscode#1512](https://github.com/OmniSharp/omnisharp-vscode/issues/1512), PR: [#872](https://github.com/OmniSharp/omnisharp-roslyn/pull/872))
* The `/findimplementations` endpoint now uses the Roslyn [`SymbolFinder.FindDerivedClassesAsync(...)`](http://source.roslyn.io/#Microsoft.CodeAnalysis.Workspaces/FindSymbols/SymbolFinder_Hierarchy.cs,dbb07fa6e6e5a08c) API and has been updated to work on CSX files. (PR: [#870](https://github.com/OmniSharp/omnisharp-roslyn/pull/870))
* Better handling when loading assemblies from an external folder, such as when a 'RoslynExtensions' path is specified. (PR: [#866](https://github.com/OmniSharp/omnisharp-roslyn/pull/866))
* Fix issue with loading Unity projects by allowing the MSBuild project system to assume that any project with the `.csproj` extension is a C# project. (PR: [#873](https://github.com/OmniSharp/omnisharp-roslyn/pull/873))
* Handle situations where `dotnet` doesn't run properly better. ([omnisharp-vscode#1532](https://github.com/OmniSharp/omnisharp-vscode/issues/1532), PR: [#879](https://github.com/OmniSharp/omnisharp-roslyn/pull/879))
* `IsSuggestionMode` property added to `/autocomplete` endpoint response to indicate when a completion list should not be committed aggressively. (PR: [#822](https://github.com/OmniSharp/omnisharp-roslyn/pull/882))

## [1.19.0] - 2017-05-19

* Update to latest MSBuild, NuGet and Roslyn packages (PR: [#867](https://github.com/OmniSharp/omnisharp-roslyn/pull/867))
* Fix a few issues with the `/autocomplete` end point (PR: [#868](https://github.com/OmniSharp/omnisharp-roslyn/pull/868))

## [1.18.1] - 2017-05-18

* Updated github api key to allow travis to publish releases

## [1.18.0] - 2017-05-17

* Use correct host object in CSX files (matching the same object used by CSI.exe). (PR #846)
* Options can now be set in an omnisharp.json to specify the Configuration (e.g. Debug) and Platform (e.g. AnyCPU) that MSBuild should use. (#202, PR: #858)
* Support for MSTest in the OmniSharp test endpoints. ([omnisharp-vscode#1482](https://github.com/OmniSharp/omnisharp-vscode/issues/1482), PR: #856)
* Fix regression introduced in v1.17.0 that could cause an `ArgumentNullException` (PR: #857)
* Fix issue with package references reporting an 'unresolved dependency' when the reference and dependency differed by case. (PR #861).
* Clean up unresolved dependency detection and improve logging to help diagnosing of dependency issues. ([omnisharp-vscode#1272](https://github.com/OmniSharp/omnisharp-vscode/issues/1272), PR: #862)
* Added new `RoslynExtensions` option to allow specifying a set of assemblies that OmniSharp will look in to find Roslyn extensions to load. (PR: #848)

## [1.17.0] - 2017-05-04

* Use Roslyn completion service for `/autocomplete` endpoint. This brings several completion improvements, such as completion for object initializer members, named parameters, CREFs, etc. (PR: #840)
* OmniSharp no longer deploys MSBuild SDKs for .NET Core projects. Instead, it uses the SDKs from the .NET Core SDK that is installed on the machine.  (#765, PR: #847)

## [1.16.1] - 2017-05-02

* Fix regression that breaks support for multi-project Unity solutions. (#839, PR: #829)
* Ensure that `/gotodefinition` and `/findsymbols` endpoints prefer the "body part" of a partial method. (PR: #838)

## [1.16.0] - 2017-04-28

* Support Metadata as Source for Go To Definition in CSX files. (#755, PR: #829)
* Cleaned up OmniSharp.Abstractions public surface area. (PR: #830)
* MSBuild project system can load referenced projects outside of OmniSharp's target directory. ([omnisharp-vscode#963](https://github.com/OmniSharp/omnisharp-vscode/issues/963), PR: #832)
* Fix 'dotnet test' support when test as "DisplayName". ([omnisharp-vscode#1426](https://github.com/OmniSharp/omnisharp-vscode/issues/1426), PR: #833)
* Fix 'dotnet test' support when multiple tests have similar names. ([omnisharp-vscode#1432](https://github.com/OmniSharp/omnisharp-vscode/issues/1432), PR: #833)
* Add support for NUnit testing in test endpoints. ([omnisharp-vscode#1434](https://github.com/OmniSharp/omnisharp-vscode/issues/1434), PR: #834)
* Add support for a few more Linux distros, namely ubuntu16.10, fedora24, and opensuse42.1. (#639, #658, PR: #835)

## [1.15.0] - 2017-04-18

* If VS 2017 is on the current machine, use the MSBuild included with VS 2017 for processing projects. ([omnisharp-vscode#1368](https://github.com/OmniSharp/omnisharp-vscode/issues/1368), PR: #818)
* Further updates to support debugging and 'dotnet test' (PR: #821, #824)

## [1.14.0] - 2017-04-06

* Properly handle package references with version ranges in .csproj (PR: #814)
* Fix regression with MSBuild project system where a project reference and a binary reference could be added for the same assembly, causing ambiguity errors (#795, PR: #815)
* More improvements for 'dotnet test' support, including a TestMessage event for test runner output and debugging support for VS Test (PR: #816)

## [1.13.0] - 2017-04-04

* Fix problem with hitting ulimit when watching for omnisharp.json file changes on OSX/Linux. (PR# 812)

## [1.12.0] - 2017-03-31

* Fix null reference exception in DotNetProjectSystem when project reference is invalid (PR: #797)
* Stop spamming log from ScriptProjectSystem on ProjectModel requests (PR: #798)
* Initial work to watch changes in omnisharp.json file while OmniSharp is running. This currently supports changes to formatting options. (PR: #804)
* Add support for /v2/runtest endpoint with .csproj-based .NET Core projects ([omnisharp-vscode#1100](https://github.com/OmniSharp/omnisharp-vscode/issues/1100), PR: #808)
* Add support for global omnisharp.json file (#717, PR# 809)

## [1.11.0] - 2017-03-10

- Code Actions now respects the formatting options that were set when OmniSharp was launched. (#759, PR: #770)
- Unsafe code is now allowed in C# scripts (PR: #781)
- C# scripting now ignores duplicated CorLibrary types, which can manifest in certain edge scenarios. (#784, PR: #785)
- Updated to RTM Roslyn and NuGet packages (PR: #791)
-	 Introduce caching for #r to avoid leaking memory in C# scripts. ([omnisharp-vscode/issues/1306](https://github.com/OmniSharp/omnisharp-vscode/issues/1306), PR: #794)

## [1.10.0] - 2017-02-11

Note: This release begins a semantic versioning scheme discussed at https://github.com/OmniSharp/omnisharp-roslyn/issues/757.

- Scripting project system now delegates more work to the Roslyn `ScriptSourceResolver`, greatly simplifying the scripting workspace, and adding support for multiple `#load` directives and live updating of `#r` and `#load` directives. ([#227](https://github.com/OmniSharp/omnisharp-roslyn/issues/227), [#689](https://github.com/OmniSharp/omnisharp-roslyn/issues/689), PR: [#760](https://github.com/OmniSharp/omnisharp-roslyn/pull/760))
- Ensure that the DotNetProjectSystem is initialized with the Roslyn `DefaultAssemblyIdentityComparer.Default` to allow assembly references to unify properly. ([omnisharp-vscode#1221](https://github.com/OmniSharp/omnisharp-vscode/issues/1221), PR: [#763](https://github.com/OmniSharp/omnisharp-roslyn/pull/763))
- Also use Roslyn's `DefaultAssemblyIdentityComparer.Default` for scripting as well. (PR: [#764](https://github.com/OmniSharp/omnisharp-roslyn/pull/764))
