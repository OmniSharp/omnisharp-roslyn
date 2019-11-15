# Changelog
All changes to the project will be documented in this file.

## [1.34.8] - not yet released
* Update to Roslyn `3.5.0-beta1-19564-02` (PR:[#1652](https://github.com/OmniSharp/omnisharp-roslyn/pull/1652))

## [1.34.7] - 2019-11-06
* Updated the embedded Mono to 6.4.0 (PR:[#1640](https://github.com/OmniSharp/omnisharp-roslyn/pull/1640))
* Update to Roslyn `3.4.0-beta3-19551-02` to align with the upcoming .NET Core 3.1 Preview 3 (PR:[#1644](https://github.com/OmniSharp/omnisharp-roslyn/pull/1644))

## [1.34.6] - 2019-10-25
* Update to Roslyn `3.4.0-beta3-19516-01` (PR:[#1634](https://github.com/OmniSharp/omnisharp-roslyn/pull/1634))
* Fixed a bug that caused CS0019 diagnostic to be erroneously reported when comparing to `default` ([#1619](https://github.com/OmniSharp/omnisharp-roslyn/issues/1619), PR:[#1634](https://github.com/OmniSharp/omnisharp-roslyn/pull/1634))
* Raised minimum Mono version to 6.4.0 to provide better .NET Core 3.0 support ([#1629](https://github.com/OmniSharp/omnisharp-roslyn/pull/1629))
* Fixed a concurrency bug in scripting/Cake support ([#1627](https://github.com/OmniSharp/omnisharp-roslyn/pull/1627))
* Correctly respect request cancellation token in metadata service ([#1631](https://github.com/OmniSharp/omnisharp-roslyn/pull/1631))

## [1.34.5] - 2019-10-08
* Fixed 1.34.4 regression that caused "go to metadata" to not work ([#1624](https://github.com/OmniSharp/omnisharp-roslyn/issues/1624), PR: [#1625](https://github.com/OmniSharp/omnisharp-roslyn/pull/1625))
* Updated the Dotnet.Script.DependencyModel and Dotnet.Script.DependencyModel.NuGet packages to version 0.50.0 adding support for .NET Core 3.0 based scripts (PR: [#1609](https://github.com/OmniSharp/omnisharp-roslyn/pull/1609))

## [1.34.4] - 2019-09-30
* Upgraded to MSBuild 16.3 and Mono MSBuild 16.3 (from Mono 6.4.0) to support .NET Core 3.0 RTM (PR: [#1616](https://github.com/OmniSharp/omnisharp-roslyn/pull/1616), [#1612](https://github.com/OmniSharp/omnisharp-roslyn/pull/1612), [#1606](https://github.com/OmniSharp/omnisharp-roslyn/pull/1606))
* Fixed behavior when there are multiple handlers are defined for a language for a given request (PR: [#1582](https://github.com/OmniSharp/omnisharp-roslyn/pull/1582))

## [1.34.3] - 2019-09-11
* Added support for `CheckForOverflowUnderflow ` in csproj files (PR: [#1587](https://github.com/OmniSharp/omnisharp-roslyn/pull/1587))
* Updated LSP libraries to 0.13 which fixes problems with clients not supporting dynamic registrations. ([#1505](https://github.com/OmniSharp/omnisharp-roslyn/issues/1505), [#1525](https://github.com/OmniSharp/omnisharp-roslyn/issues/1525), PR: [#1562](https://github.com/OmniSharp/omnisharp-roslyn/pull/1562))
* Update to Roslyn `3.4.0-beta1-19460-02` to align with the upcoming .NET Core 3.1 preview 1 (PR:[#1597](https://github.com/OmniSharp/omnisharp-roslyn/pull/1597))

## [1.34.2] - 2019-08-16
* Update to Roslyn `3.3.0-beta2-19401-05` which fixes a 1.34.1 regression resulting in StackOverflowException on code analysis of partial classes (PR: [#1579](https://github.com/OmniSharp/omnisharp-roslyn/pull/1579))
* Added support for reading C# 8.0 `Nullable` setting from csproj files (and dropped support for `NullableContextOptions` - based on the LDM decision to [rename the MSBuild property](https://github.com/dotnet/roslyn/issues/35432) ([#1573](https://github.com/OmniSharp/omnisharp-roslyn/pull/1573))

## [1.34.1] - 2019-07-31
* Added support for "sync namespace" refactoring ([#1475](https://github.com/OmniSharp/omnisharp-roslyn/issues/1475), PR: [#1563](https://github.com/OmniSharp/omnisharp-roslyn/pull/1563))
* Fixed a regression introduced in 1.32.20 which caused `AllowUnsafeCode` in csproj to also enable `TreatWarningsAsErrors` behavior ([#1565](https://github.com/OmniSharp/omnisharp-roslyn/issues/1565), PR: [#1567](https://github.com/OmniSharp/omnisharp-roslyn/pull/1567))
* Update to Roslyn `3.3.0-beta2-19376-02` (PR: [#1563](https://github.com/OmniSharp/omnisharp-roslyn/pull/1563))
* Fixed a timeout issue in large analyzer bundles (i.e. FxCop analyzers) ([#1552](https://github.com/OmniSharp/omnisharp-roslyn/issues/1552), PR: [#1566](https://github.com/OmniSharp/omnisharp-roslyn/pull/1566))

## [1.34.0] - 2019-07-15
* Added support for Roslyn code actions that normally need UI - they used to be explicitly sipped by OmniSharp, now it surfaces them with predefined defaults instead. ([#1220](https://github.com/OmniSharp/omnisharp-roslyn/issues/1220), PR: [#1406](https://github.com/OmniSharp/omnisharp-roslyn/pull/1406)) These are:
  * extract interface
  * generate constructor
  * generate overrides
  * generate *Equals* and *GetHashCode*
* Improved analyzers performance by introducing background analysis support ([#1507](https://github.com/OmniSharp/omnisharp-roslyn/pull/1507))
* According to [official Microsoft .NET Core support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core), .NET Core 1.0 and 1.1 (`project.json`-based .NET Core flavors) have reached end of life and went out of support on 27 June 2019. OmniSharp features to support that, which have been obsolete and disabled by default since version 1.32.2 (2018-08-07), are now completely removed.
* Fixed a bug where some internal services didn't respect the disabling of a project system ([#1543](https://github.com/OmniSharp/omnisharp-roslyn/pull/1543))
* Improved the MSBuild selection logic. The standalone instance inside OmniSharp is now preferred over VS2017, with VS2019 given the highest priority. This ensures that .NET Core 3.0 works correctly. It is also possible manually provide an MSBuild path using OmniSharp configuration, which is then always selected. ([#1541](https://github.com/OmniSharp/omnisharp-roslyn/issues/1541), PR: [#1545](https://github.com/OmniSharp/omnisharp-roslyn/pull/1545))
    ```JSON
        {
            "MSBuild": {
                "MSBuildOverride": {
                    "MSBuildPath": "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\MSBuild\\15.0\\Bin",
                    "Name": "vs2017 msbuild"
                }
            }
        }
    ```
* Added support for *AdditionalFiles* in csproj files ([#1510](https://github.com/OmniSharp/omnisharp-roslyn/issues/1510), PR: [#1547](https://github.com/OmniSharp/omnisharp-roslyn/pull/1547))
* Fixed a bug in *.editorconfig* where formatting settings were not correctly passed into external code fixes ([#1558](https://github.com/OmniSharp/omnisharp-roslyn/issues/1558), PR: [#1559](https://github.com/OmniSharp/omnisharp-roslyn/pull/1559))

## [1.33.0] - 2019-07-01
* Added support for `.editorconfig` files to control formatting settings, analyzers, coding styles and naming conventions. The feature is currently opt-into and needs to be enabled using OmniSharp configuration ([#31](https://github.com/OmniSharp/omnisharp-roslyn/issues/31), PR: [#1526](https://github.com/OmniSharp/omnisharp-roslyn/pull/1526))
    ```JSON
        {
            "FormattingOptions": {
                "EnableEditorConfigSupport": true
            }
        }
    ```
* Analyzers improvements (PR: [#1440](https://github.com/OmniSharp/omnisharp-roslyn/pull/1440))
	* Dynamically loaded / modifiable rulesets instead without full restart on omnisharp after every change
	* Reanalyze updated projects
	* Built-int Roslyn diagnostics can be controlled by rulesets even when analyzers are not enabled
	* Faster analysis since project isn't updated every time
	* When project is restored it is re-analyzed with correct dependencies
* Added support for various renaming options - renaming any symbol can now propagate to comments or strings, and renaming a method symbol can also rename its overloads. They can be set via OmniSharp configuration, such as `omnisharp.json` file (they are disabled by default). (PR: [#1495](https://github.com/OmniSharp/omnisharp-roslyn/pull/1495))
    ```JSON
    {
        "RenameOptions": {
            "RenameInComments": true,
            "RenameOverloads": true,
            "RenameInStrings": true
        }
    }
    ```
* Fixed a regression on declaration name completion (PR: [#1520](https://github.com/OmniSharp/omnisharp-roslyn/pull/1520))
* Update to Roslyn `3.2.0-beta4-19326-12` (PR: [#1534](https://github.com/OmniSharp/omnisharp-roslyn/pull/1534))
* Added snippets support in LSP mode (PR: [#1422](https://github.com/OmniSharp/omnisharp-roslyn/pull/1422))
* Fixed renaming in LSP mode (PR: [#1423](https://github.com/OmniSharp/omnisharp-roslyn/pull/1423))

## [1.32.20] - 2019-06-03
* Added support for `TreatWarningsAsErrors` in csproj files (PR: [#1459](https://github.com/OmniSharp/omnisharp-roslyn/pull/1459))
* Updated to Roslyn `3.2.0-beta3-19281-01` to match VS dev16.2p2 (PR: [#1511](https://github.com/OmniSharp/omnisharp-roslyn/pull/1511))
* Updated to `OmniSharp.Extensions.LanguageServer` 0.12.1 ([#1403](https://github.com/OmniSharp/omnisharp-roslyn/issues/1403), PR: [#1503](https://github.com/OmniSharp/omnisharp-roslyn/pull/1503))
* Fixed assembly redirects when shadow copying analyzers ([#1496](https://github.com/OmniSharp/omnisharp-roslyn/issues/1496), PR: [#1497](https://github.com/OmniSharp/omnisharp-roslyn/pull/1497))
* Fixed a logical bug in symbol completion (PR: [#1491](https://github.com/OmniSharp/omnisharp-roslyn/pull/1491))
* Added support for `preview` and `latestmajor` C# language versions ([#1487](https://github.com/OmniSharp/omnisharp-roslyn/issues/1487), PR: [#1488](https://github.com/OmniSharp/omnisharp-roslyn/pull/1488))

## [1.32.19] - 2019-05-01
* Updated to Roslyn `3.1.0-beta4-19251-02` (PR: [#1479](https://github.com/OmniSharp/omnisharp-roslyn/pull/1479))
* Shadow copy Roslyn analyzers in order to not lock them ([#1465](https://github.com/OmniSharp/omnisharp-roslyn/issues/1465), PR: [#1474](https://github.com/OmniSharp/omnisharp-roslyn/pull/1474))
* Fixed logging output for OmniSharp HTTP server ([#1466](https://github.com/OmniSharp/omnisharp-roslyn/issues/1446), PR: [#1456](https://github.com/OmniSharp/omnisharp-roslyn/pull/1456))
* Fixed OmniSharp hanging on wildcard Nuget package references ([omnisharp-vscode#3009](https://github.com/OmniSharp/omnisharp-vscode/issues/3009), PR: [#1473](https://github.com/OmniSharp/omnisharp-roslyn/pull/1473))
* OmniSharp now uses correct 4.7.2 framework sku to prompt for installation of .NET 4.7.2 if missing ([#1468](https://github.com/OmniSharp/omnisharp-roslyn/issues/1468), PR: [#1469](https://github.com/OmniSharp/omnisharp-roslyn/pull/1469))

## [1.32.18] - 2019-04-12
* Renamed `ProjectGuid` to `ProjectId` and no longer hash target framework names on `ProjectConfigurationMessage` (PR: [#1454](https://github.com/OmniSharp/omnisharp-roslyn/pull/1454))

## [1.32.17] - 2019-04-12
* Fixed a bug in embedded MSBuild 16 path detection (PR: [#1457](https://github.com/OmniSharp/omnisharp-roslyn/pull/1457))

## [1.32.16] - 2019-04-10
* .NET Core 3.0 support (PR: [#1450](https://github.com/OmniSharp/omnisharp-roslyn/pull/1450))
* Upgraded to Roslyn `3.1.0-beta2-19205-01` (PR: [#1448](https://github.com/OmniSharp/omnisharp-roslyn/pull/1448))
* Enabled outline support from LSP (PR: [#1411](https://github.com/OmniSharp/omnisharp-roslyn/pull/1411))

## [1.32.15] - 2019-04-09
* Startup performance improvements (PR: [#1427](https://github.com/OmniSharp/omnisharp-roslyn/pull/1427))

## [1.32.14] - 2019-04-08
* OmniSharp now targets **net472**, instead of **net461** (PR: [#1444](https://github.com/OmniSharp/omnisharp-roslyn/pull/1444))
* Upgraded OmniSharp to use Mono 5.18.1 and MSBuild `16.0.461` (PR: [#1444](https://github.com/OmniSharp/omnisharp-roslyn/pull/1444))

## [1.32.13] - 2019-04-02
* Added experimental support for Roslyn analyzers and code fixes (PR: [#1076](https://github.com/OmniSharp/omnisharp-roslyn/pull/1076))
* Included constant values in `/typelookup` responses ([omnisharp-vscode#2857](https://github.com/OmniSharp/omnisharp-vscode/issues/2857), PR: [#1420](https://github.com/OmniSharp/omnisharp-roslyn/pull/1420))
* Fixed transient documents not disappearing on project update  (PR: [#1159](https://github.com/OmniSharp/omnisharp-roslyn/pull/1159))
* When fixing usings, return namespaces associated with ambiguous result (PR: [#1169](https://github.com/OmniSharp/omnisharp-roslyn/pull/1169))
* Fixed refusing HTTP connections ([#1274](https://github.com/OmniSharp/omnisharp-roslyn/issues/1274), PR: [#1361](https://github.com/OmniSharp/omnisharp-roslyn/pull/1361))
* Fixed find references for indexer properties (PR: [#1399](https://github.com/OmniSharp/omnisharp-roslyn/pull/1399))
* Added Roslyn 'tags' to diagnostic response (PR: [#1410](https://github.com/OmniSharp/omnisharp-roslyn/pull/1410))
* Added support for `extern alias` ([omnisharp-vscode#2342](https://github.com/OmniSharp/omnisharp-vscode/issues/2342), PR: [#1409](https://github.com/OmniSharp/omnisharp-roslyn/pull/1409))

## [1.32.11] - 2019-02-27
* Updated to Roslyn `3.0.0-beta4-19126-05` to match VS 16.0p4 ([#1413](https://github.com/OmniSharp/omnisharp-roslyn/issues/1413), PR: [#1414](https://github.com/OmniSharp/omnisharp-roslyn/pull/1414))
* Added support for reading C# 8.0 `NullableContextOptions` from csproj files ([#1396](https://github.com/OmniSharp/omnisharp-roslyn/issues/1396), PR: [#1404](https://github.com/OmniSharp/omnisharp-roslyn/pull/1404))

## [1.32.10] - 2019-01-25
* Updated to Roslyn 3.0 to match [VS 2019](https://docs.microsoft.com/en-us/visualstudio/releases/2019/release-notes-preview#VS2019_Preview2) (PR: [#1391](https://github.com/OmniSharp/omnisharp-roslyn/pull/1391))
* Fixed shutdown event handling for LSP _(Contributed by [@LoneBoco](https://github.com/LoneBoco))_ ([#1113](https://github.com/OmniSharp/omnisharp-roslyn/issues/1113), PR: [#1345](https://github.com/OmniSharp/omnisharp-roslyn/pull/1345))

## [1.32.9] - 2019-1-22
* Updated to Roslyn `2.11.0-beta1-final` and initial support for C# 8 (PR: [#1365](https://github.com/OmniSharp/omnisharp-roslyn/pull/1365))
* Incorporate *IndentSwitchCaseSectionWhenBlock* into OmniSharp's formatting options. This fixes the default formatting behavior, as the setting is set to *true* by default, and still allows users to disable it if needed. ([#1351](https://github.com/OmniSharp/omnisharp-roslyn/issues/1351), PR: [#1353](https://github.com/OmniSharp/omnisharp-roslyn/pull/1353))
* Removed unused `-stdio` flag from the `StdioCommandLineApplication` (PR: [#1362](https://github.com/OmniSharp/omnisharp-roslyn/pull/1362))
* Fixed finding references to operator overloads _(Contributed by [@SirIntruder](https://github.com/SirIntruder))_ (PR: [#1371](https://github.com/OmniSharp/omnisharp-roslyn/pull/1371))
* Fixed a 1.29.0 regression that caused LSP not to work with `StdioCommandLineApplication` ([#1269](https://github.com/OmniSharp/omnisharp-roslyn/issues/1269), PR: [#1346](https://github.com/OmniSharp/omnisharp-roslyn/pull/1346))
* Improved handling of files moving on disk (PR: [#1368](https://github.com/OmniSharp/omnisharp-roslyn/pull/1368))
* Improved detection of MSBuild when multiple instances are available _(Contributed by [@johnnyasantoss ](https://github.com/johnnyasantoss))_ (PR: [#1349](https://github.com/OmniSharp/omnisharp-roslyn/pull/1349))

## [1.32.8] - 2018-11-14
* Fixed MSBuild discovery path (1.32.7 regression) (PR: [#1337](https://github.com/OmniSharp/omnisharp-roslyn/pull/1337))

## [1.32.7] - 2018-11-12
* It's now possible to override the default location of OmniSharp's global folder (%USERPROFILE%\.omnisharp or ~/.omnisharp.) with an OMNISHARPHOME environment variable (PR: [#1317](https://github.com/OmniSharp/omnisharp-roslyn/pull/1317))
* OmniSharp no longer searches for `config.json` in its source directory to load configuration (PR: [#1319](https://github.com/OmniSharp/omnisharp-roslyn/pull/1319))
* Fixed a regression introduced in 1.32.4, that prevented find symbol endpoint from working for CSX projects (PR: [#1321](https://github.com/OmniSharp/omnisharp-roslyn/pull/1321))
* Improved MSBuild discovery for future scenarios (PR: [#1328](https://github.com/OmniSharp/omnisharp-roslyn/pull/1328))
* Enabled setting customer OmniSharp home directory (PR: [#1317](https://github.com/OmniSharp/omnisharp-roslyn/pull/1317))
* Made detection of .sln files more accurate  _(Contributed by [@itn3000](https://github.com/itn3000))_ (PR: [#1320](https://github.com/OmniSharp/omnisharp-roslyn/pull/1320))
* Improved reliability of document management subsystem _(Contributed by [@NTaylorMullen](https://github.com/NTaylorMullen))_ (PR: [#1330](https://github.com/OmniSharp/omnisharp-roslyn/pull/1330))
* Use Roslyn's new `FindSourceDeclarationsWithPatternAsync` API in symbol finder _(Contributed by [@SirIntruder](https://github.com/SirIntruder))_ (PR: [#1304](https://github.com/OmniSharp/omnisharp-roslyn/pull/1304))
* Fix `FindImplementationService` not finding all implementations of the partial class _(Contributed by [@SirIntruder](https://github.com/SirIntruder))_ (PR: [#1318](https://github.com/OmniSharp/omnisharp-roslyn/pull/1318))

## [1.32.6] - 2018-10-02
* Fixed a bug where virtual C# documents would not get promoted to be a part of a project. (PR: [#1306](https://github.com/OmniSharp/omnisharp-roslyn/pull/1306)).
* Added MinFilterLength to configure the number of characters a user must type in for FindSymbolRequest command to return any results (default is 0 to preserve existing behavior). Additionally added MaxItemsToReturn for configuring maximum number of items returned by the FindSymbolsRequestAPI.(PR: [#1284](https://github.com/OmniSharp/omnisharp-roslyn/pull/1284)).
* Fixed issue where `/codestructure` endpoint did not return enum members. (PR: [#1285](https://github.com/OmniSharp/omnisharp-roslyn/pull/1285))
* Fixed issue where `/findimplemenations` endpoint did not return overridden members in derived types (PR: [#1302](https://github.com/OmniSharp/omnisharp-roslyn/pull/1302))

## [1.32.3] - 2018-08-28
* Added support for files without a project. (PR: [#1252](https://github.com/OmniSharp/omnisharp-roslyn/pull/1252))
* Fixed a bug where `*.rsp`-based scripting references where not exposed in the Workspace information endpoint (PR: [#1272](https://github.com/OmniSharp/omnisharp-roslyn/pull/1272))

## [1.32.2] - 2018-08-07
* OmniSharp now targets **net461**, instead of **net46** (PR: [#1237](https://github.com/OmniSharp/omnisharp-roslyn/pull/1237))
* Added new `/codestructure` endpoint which serves a replacement for the `/currentfilemembersastree` endpoint. The new endpoint has a cleaner design, properly supports all C# types and members, and supports more information, such as accessibility, static vs. instance, etc. (PRs: [#1211](https://github.com/OmniSharp/omnisharp-roslyn/pull/1211) [#1217](https://github.com/OmniSharp/omnisharp-roslyn/pull/1217))
* Fixed a bug where language services for newly created CSX files were not provided if no CSX files existed at the moment OmniSharp was started ([#1199](https://github.com/OmniSharp/omnisharp-roslyn/issues/1199), PR: [#1210](https://github.com/OmniSharp/omnisharp-roslyn/pull/1210))
* The legacy project.json support is now disabled by default, allowing OmniSharp to start up a bit faster for common scenarios. If you wish to enable project.json support, add the following setting to your `omnisharp.json` file. (PR: [#1194](https://github.com/OmniSharp/omnisharp-roslyn/pull/1194))

    ```JSON
    {
        "dotnet": {
            "enabled": false
        }
    }
    ```
* Added support for code actions in `.cake` files. ([#1205](https://github.com/OmniSharp/omnisharp-roslyn/issues/1205), PR: [#1212](https://github.com/OmniSharp/omnisharp-roslyn/pull/1212))
* Added a new `/blockstructure` endpoint that returns the spans of the C# code blocks (usings, namespaces, methods, etc.) in a file. (PRs: [#1209](https://github.com/OmniSharp/omnisharp-roslyn/pull/1209) [#1231](https://github.com/OmniSharp/omnisharp-roslyn/pull/1231))
* Fixed bug where find usages returned usages from loaded `.cake` files even though `OnlyThisFile` was set to `true` in the request. ([#1204](https://github.com/OmniSharp/omnisharp-roslyn/issues/1204), PR: [#1213](https://github.com/OmniSharp/omnisharp-roslyn/pull/1213))
* Performance improvements for line mappings when working with `.cake` files. (PR: [#1226](https://github.com/OmniSharp/omnisharp-roslyn/pull/1226))
* Fixed a bug where a new debug session could not be started after a previous one failed due to build error. (PR: [#1239](https://github.com/OmniSharp/omnisharp-roslyn/pull/1239))
* Upgraded dependencies (PR: [#1237](https://github.com/OmniSharp/omnisharp-roslyn/pull/1237))
  * Upgraded to .NET Core SDK 2.1.505
  * Upgraded to Microsoft.AspNetCore.* version 2.1.1
  * Upgraded to Microsoft.Extensions.* version 2.1.1
  * Upgraded to MSBuild 15.7
  * Upgraded to Roslyn 2.8.2

## [1.31.1] - 2018-05-28
* Fixed bug where diagnostics from loaded `.cake` files was shown in the current file. (PR: [#1201](https://github.com/OmniSharp/omnisharp-roslyn/pull/1201))

## [1.31.0] - 2018-05-29
* Update to Roslyn 2.8.0 packages, adding support for C# 7.3. (PR: [#1182](https://github.com/OmniSharp/omnisharp-roslyn/pull/1182))
* MSBuild project system no longer stops when a project fails to load. (PR: [#1181](https://github.com/OmniSharp/omnisharp-roslyn/pull/1181))
* Fixed null-reference exception that could be thrown during MSBuild discovery. ([#1188](https://github.com/OmniSharp/omnisharp-roslyn/issues/1188), PR: [#1189](https://github.com/OmniSharp/omnisharp-roslyn/issues/1188))
* Fixed an issue where referenced projects outside of OmniSharp's target path/solution would not be evaluated properly if they were multi-targeted (e.g. contained `<TargetFrameworks>`), which could result in downstream failures. ([omnisharp-vscode#2295](https://github.com/OmniSharp/omnisharp-vscode/issues/2295), PR: [#1195](https://github.com/OmniSharp/omnisharp-roslyn/pull/1195))
* Removed logic that set `MSBuildSDKsPath` environment variable before loading a project. This environment variable overrides normal MSBuild SDK resolution, which breaks resolution for custom MSBuild SDKs (for more information on MSBuild SDKs, see the [documentation](https://docs.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk#how-project-sdks-are-resolved)). ([#1190](https://github.com/OmniSharp/omnisharp-roslyn/issues/1190), PR: [#1192](https://github.com/OmniSharp/omnisharp-roslyn/pull/1192))
    * **Breaking Change**: Removing this logic means that OmniSharp will no longer load .NET Core projects that target a .NET Core SDK with a version <= 1.0.3 by default. If you need to restore this behavior, you can set the following option in an `omnisharp.json` configuration file:

        ```JSON
        {
            "MSBuild": {
                "UseLegacySdkResolver": true
            }
        }
        ```
        See [Configuration Options](https://github.com/OmniSharp/omnisharp-roslyn/wiki/Configuration-Options) for more details on `omnisharp.json`.
* Support `/rename` endpoint in `.cake` files.
* Support custom `.rsp` files in scripting. It is now possible to use `omnisharp.json` to define a path to an `.rsp` file, containing predefined namespaces and assembly references, and OmniSharp will respect those as part of its language services for CSX files. For example, given the following `.rsp` file:

    ```
    /r:bin/FakeLib.dll
    /r:bin/FSharp.Core.dll
    /r:bin/FSharpx.Extras.dll
    /u:Fake
    /u:FSharpx
    /u:System.Linq
    /u:System.IO
    ```
    and the following `omnisharp.json`:

    ```
    {
        "Script": {
            "RspFilePath": "path/to/my.rsp"
        }
    }
    ```
    OmniSharp will automatically include the predefined DLLs and namespaces in the language services for all the scripts in the given folder (in case of a local `omnisharp.json`) or on the machine (in case of a global `omnisharp.json`). Note that the reference to `mscorlib`/`System.Runtime` is always there anyway and doesn't need to be specified again in the `.rsp` file. ([#1024](https://github.com/OmniSharp/omnisharp-roslyn/issues/1024), PR: [#1112](https://github.com/OmniSharp/omnisharp-roslyn/issues/1112))
    * Note that the reference to `mscorlib`/`System.Runtime` is always there anyway and doesn't need to be specified again in the `.rsp` file
    * only imports and references are supported as part of the `.rsp` file (scripting doesn't support other compiler settings passed using the `.rsp` file). In the future, depending on whether the [feature is available in Roslyn](https://github.com/dotnet/roslyn/issues/23421), OmniSharp may also support defining a scripting globals type via `.rsp` file.
* `.cake` files are now parsed using the C# version `Latest` rather than `Default`, to match the runtime behavior of Cake. (PR: [#1201](https://github.com/OmniSharp/omnisharp-roslyn/pull/1201))
* Updated `DotNetTest` result to include messages from stdout and stderr. (PR: [#1203](https://github.com/OmniSharp/omnisharp-roslyn/pull/1203))

## [1.30.1] - 2018-05-11
* Fixed a 1.30.0 regression that prevented the script project system from working on Unix-based systems (PR: [#1185](https://github.com/OmniSharp/omnisharp-roslyn/pull/1185))

## [1.30.0] - 2018-4-30
* Updated to Roslyn 2.7.0 packages (PR: [#1132](https://github.com/OmniSharp/omnisharp-roslyn/pull/1132))
* Ensure that the lower assembly versions are always superseded in C# scripts (PR: [#1103](https://github.com/OmniSharp/omnisharp-roslyn/pull/1103))
* Updated OmniSharp.Script to DotNet.Script.DependencyModel 0.6.0 (PR: [#1150](https://github.com/OmniSharp/omnisharp-roslyn/pull/1150))
* It is now possible to define the default target framework for C# scripts in the OmniSharp configuration (PR: [#1154](https://github.com/OmniSharp/omnisharp-roslyn/pull/1154))
* Upgraded embedded Mono and MSBuild to 5.10.1.20 (PRs: #[1137](https://github.com/OmniSharp/omnisharp-roslyn/pull/1137), #[1145](https://github.com/OmniSharp/omnisharp-roslyn/pull/1145))
* Fixed issue where generate type refactoring could not generate new files ([omnisharp-vscode#2112](https://github.com/OmniSharp/omnisharp-vscode/issues/2112), PR: [#1143](https://github.com/OmniSharp/omnisharp-roslyn/pull/1143))
* Added detailed project information output at debug log level (PR: [#1151](https://github.com/OmniSharp/omnisharp-roslyn/pull/1151))
* Set MSBuild property to allow the XAML markup compiler task to run (PR: [#1157](https://github.com/OmniSharp/omnisharp-roslyn/pull/1157))
* Added support for excluding search paths via globbing patterns ([#896](https://github.com/OmniSharp/omnisharp-roslyn/issues/896), PR: [#1161](https://github.com/OmniSharp/omnisharp-roslyn/pull/1161))
* Improved versioning reporting for VS preview consoles (PR: [#1166](https://github.com/OmniSharp/omnisharp-roslyn/pull/1166))

## [1.29.1] - 2018-2-12
* Fixed duplicate diagnostics in C# ([omnisharp-vscode#1830](https://github.com/OmniSharp/omnisharp-vscode/issues/1830), PR: [#1107](https://github.com/OmniSharp/omnisharp-roslyn/pull/1107))

## [1.29.0] - 2018-1-29
* Updated to Roslyn 2.6.1 packages - C# 7.2 support (PR: [#1055](https://github.com/OmniSharp/omnisharp-roslyn/pull/1055))
* Shipped Language Server Protocol support in box.  (PR: [#969](https://github.com/OmniSharp/omnisharp-roslyn/pull/969))
  - Additional information and features tracked at [#968](https://github.com/OmniSharp/omnisharp-roslyn/issues/968)
* Fixed locating Visual Studio with more than one installation (PR: [#1063](https://github.com/OmniSharp/omnisharp-roslyn/pull/1063))
* Do not crash when encoutering Legacy ASP.NET Website projects ([#1036](https://github.com/OmniSharp/omnisharp-roslyn/issues/1036), PRs: [#1066](https://github.com/OmniSharp/omnisharp-roslyn/pull/1066), [#1084](https://github.com/OmniSharp/omnisharp-roslyn/pull/1084))
* Improvements to the the structured documentation returned by the /typelookup endpoint ([#1046](https://github.com/OmniSharp/omnisharp-roslyn/issues/1046), [omnisharp-vscode#1057](https://github.com/OmniSharp/omnisharp-vscode/issues/1057),  PRs: [#1062](https://github.com/OmniSharp/omnisharp-roslyn/pull/1062) [#1064](https://github.com/OmniSharp/omnisharp-roslyn/pull/1064))
* Allowed specifying DLLs file paths for plugin loading (PR: [#1069](https://github.com/OmniSharp/omnisharp-roslyn/pull/1069))
* Improved http server performance (PR: [#1073](https://github.com/OmniSharp/omnisharp-roslyn/pull/1073))
* Added attribute span to file ([omnisharp-vscode#429](https://github.com/OmniSharp/omnisharp-vscode/issues/429), PR: [#1075](https://github.com/OmniSharp/omnisharp-roslyn/pull/1075))
* Order Code Actions according by `ExtensionOrderAttribute` ([omnisharp-roslyn#748](https://github.com/OmniSharp/omnisharp-roslyn/issues/758), PR: [#1078](https://github.com/OmniSharp/omnisharp-roslyn/pull/1078))
* Disabled Go To Definition on property get/set keywords  ([omnisharp-vscode#1949](https://github.com/OmniSharp/omnisharp-vscode/issues/1949), PR: [#1086](https://github.com/OmniSharp/omnisharp-roslyn/pull/1086/files))
* Disabled exceptions on assembly load failure (PR: [#1072](https://github.com/OmniSharp/omnisharp-roslyn/pull/1072))
* Added structured documentation to signature help ([omnisharp-vscode#1940](https://github.com/OmniSharp/omnisharp-vscode/issues/1940), PR: [#1085](https://github.com/OmniSharp/omnisharp-roslyn/pull/1085))
* Added /runalltests and /debugalltests endpoints to run or debug all the tests in a class ([omnisharp-vscode#1969](https://github.com/OmniSharp/omnisharp-vscode/pull/1961), PR: [#1961](https://github.com/OmniSharp/omnisharp-vscode/pull/1961))

## [1.28.0] - 2017-12-14

* Fixed issue with loading XML documentation for `#r` assembly references in CSX scripts ([#1026](https://github.com/OmniSharp/omnisharp-roslyn/issues/1026), PR: [#1027](https://github.com/OmniSharp/omnisharp-roslyn/pull/1027))
* Updated the `/v2/runcodeaction` end point to return document "renames" and "opens" that a code action might perform. (PR: [#1023](https://github.com/OmniSharp/omnisharp-roslyn/pull/1023))
* Corrected issue where MSBuild discovery would pick instances of Visual Studio 2017 that did not have Roslyn installed. ([#1031](https://github.com/OmniSharp/omnisharp-roslyn/issues/1031), PR: [#1032](https://github.com/OmniSharp/omnisharp-roslyn/pull/1032))
* Updated `/codecheck` endpoint to return diagnostic IDs. (PR: [#1034](https://github.com/OmniSharp/omnisharp-roslyn/pull/1034))
* Updated OmniSharp.Script to DotNet.Script.DependencyModel 0.3.0 (PR: [#1035](https://github.com/OmniSharp/omnisharp-roslyn/pull/1035))
* Fixed scripting suppot to not load the same assembly name multiple times ([dotnet-script#194](https://github.com/filipw/dotnet-script/issues/194), PR: [#1037](https://github.com/OmniSharp/omnisharp-roslyn/pull/1037))
* STDIO requests and responses are now pretty-printed during logging. (PR: [#1040](https://github.com/OmniSharp/omnisharp-roslyn/pull/1040))
* Several fixes to the `/signaturehelp` endpoint to return correct signatures in more locations. ([omnisharp-vscode#1440](https://github.com/OmniSharp/omnisharp-vscode/issues/1440), [omnisharp-vscode#1664](https://github.com/OmniSharp/omnisharp-vscode/issues/1664) [omnisharp-vscode#1715](https://github.com/OmniSharp/omnisharp-vscode/issues/1715), PRs: [#1030](https://github.com/OmniSharp/omnisharp-roslyn/pull/1030), [#1052](https://github.com/OmniSharp/omnisharp-roslyn/pull/1052))
* Updated `/typelookup` endpoint to include structured object representing the various sections of an XML doc comment. ([omnisharp-vscode#1057](https://github.com/OmniSharp/omnisharp-vscode/issues/1057), PR: [#1038](https://github.com/OmniSharp/omnisharp-roslyn/pull/1038))
* Ensure the correct range is used when formatting a span that includes preceding whitespace. ([omnisharp-vscode#214](https://github.com/OmniSharp/omnisharp-vscode/issues/214), PR: [#1043](https://github.com/OmniSharp/omnisharp-roslyn/pull/1043))
* Fix issue in Cake project system where it attempted to create MetadataReferences for files that don't exist. (PR: [#1045](https://github.com/OmniSharp/omnisharp-roslyn/pull/1045))
* Improvements to the Cake bakery resolver to resolve from both OmniSharp options and PATH. (PR: [#1047](https://github.com/OmniSharp/omnisharp-roslyn/pull/1047))
* Ensure that the Cake.Core assembly is not locked on disk when loading the host object type. (PR: [#1044](https://github.com/OmniSharp/omnisharp-roslyn/pull/1044))
* Added internal support for watching for changes by file extension. (PR: [#1053](https://github.com/OmniSharp/omnisharp-roslyn/pull/1053))
* Watch added/removed .cake-files and update workspace accordingly. (PR: [#1054] (https://github.com/OmniSharp/omnisharp-roslyn/pull/1054))
* Watch added/removed .csx-files and update workspace accordingly. (PR: [#1056] (https://github.com/OmniSharp/omnisharp-roslyn/pull/1056))
* Updated `Cake.Scripting.Transport` dependencies to 0.2.0 in order to improve performance when working with Cake files. (PR: [#1057](https://github.com/OmniSharp/omnisharp-roslyn/pull/1057))

## [1.27.2] - 2017-11-10

* Addressed problem with Sdk-style projects not being loaded properly in certain cases. ([omnisharp-vscode#1846](https://github.com/OmniSharp/omnisharp-vscode/issues/1846), [omnisharp-vscode#1849](https://github.com/OmniSharp/omnisharp-vscode/issues/1849), PR: [#1021](https://github.com/OmniSharp/omnisharp-roslyn/pull/1021))

## [1.27.1] - 2017-11-09

* Fix to allow signature help return results for attribute constructors. ([omnisharp-vscode#1814](https://github.com/OmniSharp/omnisharp-vscode/issues/1814), PR: [#1007](https://github.com/OmniSharp/omnisharp-roslyn/pull/1007))
* Make `--zero-based-indices` command line argument work again. (PR: [#1015](https://github.com/OmniSharp/omnisharp-roslyn/pull/1015))
* Fix serious regression introduced in 1.27.0 that causes projects to fail to load on macOS or Linux. (PR: [#1017](https://github.com/OmniSharp/omnisharp-roslyn/pull/1017)]
* Fixed issue with discovering MSBuild under Mono even when it is missing. ([#1011](https://github.com/OmniSharp/omnisharp-roslyn/issues/1011), PR: [#1018](https://github.com/OmniSharp/omnisharp-roslyn/pull/1018))
* Fixed issue to not use Visual Studio 2017 MSBuild if it is from VS 2017 RTM. ([#1014](https://github.com/OmniSharp/omnisharp-roslyn/issues/1014), PR: [#1018](https://github.com/OmniSharp/omnisharp-roslyn/pull/1018))

## [1.27.0] - 2017-11-07

* Significant changes made to the MSBuild project system that fix several issues. (PR: [#1003](https://github.com/OmniSharp/omnisharp-roslyn/pull/1003))
  * Package restores are now better detected. ([omnisharp-vscode#1583](https://github.com/OmniSharp/omnisharp-vscode/issues/1583), [omnisharp-vscode#1661](https://github.com/OmniSharp/omnisharp-vscode/issues/1661), [omnisharp-vscode#1785](https://github.com/OmniSharp/omnisharp-vscode/issues/1785))
  * Metadata references are properly removed from projects in the OmniSharpWorkspace when necessary.
  * File watching/notification now handles paths case-insensitively.
  * MSBuild project system now loads projects asynchronously after OmniSharp has finished initializing.

## [1.26.3] - 2017-11-10

* Addressed problem with Sdk-style projects not being loaded properly in certain cases. ([omnisharp-vscode#1846](https://github.com/OmniSharp/omnisharp-vscode/issues/1846), [omnisharp-vescode#1849](https://github.com/OmniSharp/omnisharp-vscode/issues/1849), PR: [#1021](https://github.com/OmniSharp/omnisharp-roslyn/pull/1021))

## [1.26.2] - 2017-11-09

* Fixed issue with discovering MSBuild under Mono even when it is missing. ([#1011](https://github.com/OmniSharp/omnisharp-roslyn/issues/1011), PR: [#1016](https://github.com/OmniSharp/omnisharp-roslyn/pull/1016))
* Fixed issue to not use Visual Studio 2017 MSBuild if it is from VS 2017 RTM. ([#1014](https://github.com/OmniSharp/omnisharp-roslyn/issues/1014), PR: [#1016](https://github.com/OmniSharp/omnisharp-roslyn/pull/1016))

## [1.26.1] - 2017-11-04

* Fixed issue with locating MSBuild when running OmniSharp on Mono on Windows. (PR: [#1001](https://github.com/OmniSharp/omnisharp-roslyn/pull/1001))
* Fixed problem where the Antlr4.CodeGenerator Nuget package would not generate files during OmniSharp design-time build. ([omnisharp-vscode#1822](https://github.com/OmniSharp/omnisharp-vscode/issues/1822), PR: [#1002](https://github.com/OmniSharp/omnisharp-roslyn/pull/1002))
* Fixed issue where a C# project referencing a non-C# project would cause the referenced project to be loaded (causing OmniSharp to potentially treat it as C#!). ([omnisharp-vscode#371](https://github.com/OmniSharp/omnisharp-vscode/issues/371), [omnisharp-vscode#1829](https://github.com/OmniSharp/omnisharp-vscode/issues/1829), PR: [#1005](https://github.com/OmniSharp/omnisharp-roslyn/pull/1005))

## [1.26.0] - 2017-10-27

* Cake support added! (PR: [#932](https://github.com/OmniSharp/omnisharp-roslyn/pull/932))
* csproj-based C# scripts are now supported. (PR: [#980](https://github.com/OmniSharp/omnisharp-roslyn/pull/980))
* Updated to Roslyn 2.4.0 packages. (PR: [#998](https://github.com/OmniSharp/omnisharp-roslyn/pull/998))
* MSBuild SdkResolvers now ship with OmniSharp, allowing it to correctly locate the .NET Core SDK for a particular project. (PR: [#974](https://github.com/OmniSharp/omnisharp-roslyn/pull/974))
* Big improvements in OmniSharp's process for located MSBuild 15.0 and MSBuild toolsets on the machine. (PR: [#988](https://github.com/OmniSharp/omnisharp-roslyn/pull/988)
* Updated `/filesChanged` endpoint to allow the client to describe the type of file change (create, delete or change). If a client provides this extra information, files are properly removed and added to the workspace. (PR: [#987](https://github.com/OmniSharp/omnisharp-roslyn/pull/987))
* Improved filtering in `/findsymbols` to include substring matches. (PR: [#990](https://github.com/OmniSharp/omnisharp-roslyn/pull/990))
* `/autocomplete` end point now takes a `TriggerCharacter` property that can be used to trigger completion after a SPACE character. (PR: [#975](https://github.com/OmniSharp/omnisharp-roslyn/pull/975))
* Fix issue with port number not being used when passed as command line argument. (PR: [#971](https://github.com/OmniSharp/omnisharp-roslyn/pull/971))

## [1.25.0] - 2017-09-22

* Major refactoring to split OmniSharp into two servers for each supported protocol: one for HTTP, and one for STDIO. (PR: [#854](https://github.com/OmniSharp/omnisharp-roslyn/pull/854))
* Fixed a bug where language version was not correctly read from .csproj projects. ([#961](https://github.com/OmniSharp/omnisharp-roslyn/issues/961))
* Fixed issue where signing key file was not propogated to OmniSharpWorkspace correctly.

## [1.24.0] - 2017-08-31

* Fixed a bug where an external code action DLL with missing dependencies would crash OmniSharp.
* When running a test via 'dotnet vstest' support, pass "--no-restore" when building with the .NET CLI to ensure that implicit restore does not run, making build a bit faster. ([#942](https://github.com/OmniSharp/omnisharp-roslyn/issues/942))
* Add support for specifying the 'TargetFrameworkVersion' to the 'dotnet vstest' endpoints. ([#944](https://github.com/OmniSharp/omnisharp-roslyn/issues/944))
* Do not throw an exception when attempting to "go to definition" on a namespace

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
-     Introduce caching for #r to avoid leaking memory in C# scripts. ([omnisharp-vscode/issues/1306](https://github.com/OmniSharp/omnisharp-vscode/issues/1306), PR: #794)

## [1.10.0] - 2017-02-11

Note: This release begins a semantic versioning scheme discussed at https://github.com/OmniSharp/omnisharp-roslyn/issues/757.

- Scripting project system now delegates more work to the Roslyn `ScriptSourceResolver`, greatly simplifying the scripting workspace, and adding support for multiple `#load` directives and live updating of `#r` and `#load` directives. ([#227](https://github.com/OmniSharp/omnisharp-roslyn/issues/227), [#689](https://github.com/OmniSharp/omnisharp-roslyn/issues/689), PR: [#760](https://github.com/OmniSharp/omnisharp-roslyn/pull/760))
- Ensure that the DotNetProjectSystem is initialized with the Roslyn `DefaultAssemblyIdentityComparer.Default` to allow assembly references to unify properly. ([omnisharp-vscode#1221](https://github.com/OmniSharp/omnisharp-vscode/issues/1221), PR: [#763](https://github.com/OmniSharp/omnisharp-roslyn/pull/763))
- Also use Roslyn's `DefaultAssemblyIdentityComparer.Default` for scripting as well. (PR: [#764](https://github.com/OmniSharp/omnisharp-roslyn/pull/764))
