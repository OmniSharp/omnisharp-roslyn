# Requirements

The .NET SDK version selected by `global.json` is required on every platform.

## Windows

In order to build OmniSharp, the [.NET 4.7.2 targeting pack](https://dotnet.microsoft.com/download/dotnet-framework/thank-you/net472-developer-pack-offline-installer) must be installed if it isn't already.

## macOS

**Mono 6.6.0** or greater is required to build and test the `net472` target. You can install this using the latest [.pkg](http://www.mono-project.com/download/#download-mac) or install it via [Homebrew](https://brew.sh/):

```
brew update
brew install mono
brew install homebrew/cask/mono-mdk
```

## Linux

Because OmniSharp uses the .NET SDK as part of the build, not all Linux distributions are supported. Check the [.NET Linux dependencies](https://learn.microsoft.com/dotnet/core/install/linux) for supported distributions.

**Mono 6.6.0** or greater is required to build and test the `net472` target. Each distribution or derivative has its own installation instructions on the [Mono download page](http://www.mono-project.com/download/#download-lin).

# Usage

Run `build.(ps1|sh)` with the desired set of arguments (see below for options).
The build script itself is `build.cake`, written in C# using the [Cake build automation system](http://cakebuild.net/).
All build related activites should be encapsulated in this file for cross-platform access.

# Arguments

Note: The arguments below should prefixed with a single hyphen on Windows (PowerShell-style) and a double-hyphen on OSX/Linux.

`-target TargetName`: The name of the build task/target to execute (see below for listing and details).
Defaults to `Default`.

`-configuration (Release|Debug)`: The configuration to build.
Defaults to `Debug`.

`-test-framework (net10.0|net472)`: The target framework to use when running tests.
Defaults to `net472`.

`-install-path Path`: Path used for the **Install** target.
Defaults to `(%USERPROFILE%|$HOME)/.omnisharp`

`-publish-all`: Publishes all platforms for the current OS. On Windows, specifying this argument would produce win7-x86, win7-x64, and win10-arm64 builds. On OSX/Linux, this argument causes osx, linux-arm64, linux-x64, linux-musl-x64, linux-musl-arm64 and linux-bionic-arm64 builds to be published.

`-archive`: Enable the generation of publishable archives after a build.

`-use-global-dotnet-sdk`: Use SDKs already installed on the machine instead of installing the compatibility SDK versions listed in `build.json`.

Note: On macOS/Linux, be sure to pass the arguments above with a double hyphen! (e.g. `--target TargetName`).

# Targets

**Default**: Alias for All.

**All**: Full build including testing.

**Quick**: Local build which skips all testing.

**Install**: Same as quick, but installs the generated binaries into `install-path`.

**PrepareTests**: Build the solution and prepare all test assets without running tests.

**RunTests**: Run previously built tests for `test-framework`. This is useful when retrying tests without rebuilding.

# Configuration files

## build.json

A number of build-related options, including folder names for different entities. Interesting options:

**DotNetInstallScriptURL**: The URL where the .NET SDK install script is located.
Can be used to pin to a specific script version, if a breaking change occurs.

**DotNetChannel**: The .NET Core SDK channel used for retreiving the tools.

**DotNetVersions**: Compatibility SDK versions needed by the test assets. The primary build SDK is selected by `global.json`.

# Artifacts generated

-   OmniSharp binaries under `artifacts/publish/<host project>/<platform>/`
-   Scripts to run OmniSharp under `artifacts/scripts/`
    -   These scripts are updated for every build and every install.
    -   The scripts point to the installed binary after and install, otherwise just the build folder (reset if a new build occurs without an install).
-   Test logs in `artifacts/logs`
-   Archived binaries in `artifacts/package` (only if `-archive` used on command line)
