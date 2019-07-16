# Requirements

## Windows

In order to build OmniSharp, the [.NET 4.6 targeting pack](http://go.microsoft.com/fwlink/?LinkId=528261) must be installed if it isn't already.

## macOS

**Mono 5.2.0** or greater is required. You can install this using the latest [.pkg](http://www.mono-project.com/download/#download-mac) or install it view [Homebrew](https://brew.sh/):

```
brew update
brew install mono
brew install caskroom/cask/mono-mdk
```

## Linux

Because OmniSharp uses the .NET Core SDK as part of the build, not all Linux distros are supported. A good rule of thumb is to check the list [here](https://www.microsoft.com/net/download/linux) to see if your particular distro is supported.

**Mono 5.2.0** or greater is required. Each distro or derivative has it's own set of instructions for installing Mono which you can find [here](http://www.mono-project.com/download/#download-lin). Be sure to install `msbuild` as well, which may be a separate package.

# Usage

Run `build.(ps1|sh)` with the desired set of arguments (see below for options).
The build script itself is `build.cake`, written in C# using the [Cake build automation system](http://cakebuild.net/).
All build related activites should be encapsulated in this file for cross-platform access.

# Arguments

Note: The arguments below should prefixed with a single hyphen on Windows (PowerShell-style) and a double-hyphen on OSX/Linux.

  `-target TargetName`: The name of the build task/target to execute (see below for listing and details).
    Defaults to `Default`.

  `-configuration (Release|Debug)`: The configuration to build.
    Defaults to `Release`.

  `-test-configuration (Release|Debug)`: The configuration to use for the unit tests.
    Defaults to `Debug`.

  `-install-path Path`: Path used for the **Install** target.
    Defaults to `(%USERPROFILE%|$HOME)/.omnisharp`

  `-all-publish`: Publishes all platforms for the current OS. On Windows, specifying this argument would produce win7-x86 and win7-x64 builds. On OSX/Linux, this argument causes osx, linux-x86, and linux-x64 builds to be published.

  `-archive`: Enable the generation of publishable archives after a build.

Note: On macOS/Linux, be sure to pass the arguments above with double slashes! (e.g. `--target TargetName`).

# Targets

**Default**: Alias for All.

**All**: Full build including testing.

**Quick**: Local build which skips all testing.

**Install**: Same as quick, but installs the generated binaries into `install-path`.

# Configuration files

## build.json

A number of build-related options, including folder names for different entities. Interesting options:

**DotNetInstallScriptURL**: The URL where the .NET SDK install script is located.
  Can be used to pin to a specific script version, if a breaking change occurs.

**DotNetChannel**: The .NET Core SDK channel used for retreiving the tools.

**DotNetVersion**: The .NET Core SDK version used for the build. Can be used to pin to a specific version.
  Using the string `Latest` will retrieve the latest version.

# Artifacts generated

* OmniSharp binaries for specified runtimes `artifacts/publish/OmniSharp/{platform}/`
* Scripts to run OmniSharp at `scripts/OmniSharp(.Core)(.cmd)`
  * These scripts are updated for every build and every install.
  * The scripts point to the installed binary after and install, otherwise just the build folder (reset if a new build occurs without an install).
* Test logs in `artifacts/logs`
* Archived binaries in `artifacts/package` (only if `-archive` used on command line)
