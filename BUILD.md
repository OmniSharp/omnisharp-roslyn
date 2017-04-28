# Requirements

## Windows

In order to build OmniSharp, the [.NET 4.6 targeting pack](http://go.microsoft.com/fwlink/?LinkId=528261) must be installed if it isn't already.

## macOS

**Mono 4.8.0** or greater is required. You can install this using the latest [.pkg](http://www.mono-project.com/download/#download-mac) or install it view [Homebrew](https://brew.sh/):

```
brew update
brew install mono
brew install caskroom/cask/mono-mdk
```

## Linux

Because OmniSharp uses the .NET Core SDK as part of the build, not all Linux distros are supported. A good rule of thumb is to check the list [here](https://www.microsoft.com/net/download/linux) to see if your particular distro is supported.

**Mono 4.8.0** or greater is required. Each distro or derivative has it's own set of instructions for installing Mono which you can find [here](http://www.mono-project.com/download/#download-lin).

In addition, the `msbuild` package must be installed. On Debian, Ubuntu, and derivatives, this can be achieved by first adding the Mono Project GPG signing key and package repository using the instructions [here](http://www.mono-project.com/docs/getting-started/install/linux/#debian-ubuntu-and-derivatives). Then, install msbuild via apt-get.

```
sudo apt-get install msbuild
```

# Usage

Run `build.(ps1|sh)` with the desired set of arguments (see below for options).
The build script itself is `build.cake`, written in C# using the [Cake build automation system](http://cakebuild.net/).
All build related activites should be encapsulated in this file for cross-platform access.

# Arguments

  `-target TargetName`: The name of the build task/target to execute (see below for listing and details).
    Defaults to `Default`.

  `-configuration (Release|Debug)`: The configuration to build.
    Defaults to `Release`.

  `-test-configuration (Release|Debug)`: The configuration to use for the unit tests.
    Defaults to `Debug`.

  `-install-path Path`: Path used for the **Install** target.
    Defaults to `(%USERPROFILE%|$HOME)/.omnisharp/local`

  `-archive`: Enable the generation of publishable archives after a build.

Note: On macOS/Linux, be sure to pass the arguments above with double slashes! (e.g. `--target TargetName`).

# Targets

**Default**: Alias for Local.

**Local**: Full build including testing for the machine-local runtime.

**All**: Same as local, but targeting all runtimes selected by `PopulateRuntimes` in `build.cake`.
  Currently configured to also build for a 32-bit Windows runtime on Windows machines.
  No additional runtimes are currently selected on non-Windows machines.

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

**LegacyDotNetVersion**: The .NET Core SDK version used to restore packages for various test assets that are project.json-based.

# Artifacts generated

* Binaries of OmniSharp and its libraries built for the local machine in `artifacts/publish/OmniSharp/default/{framework}/`
* Scripts to run OmniSharp at `scripts/OmniSharp(.Core)(.cmd)`
  * These scripts are updated for every build and every install.
  * The scripts point to the installed binary after and install, otherwise just the build folder (reset if a new build occurs without an install).
* Binaries of OmniSharp and its libraries cross-compiled for other runtimes (if selected in **PopulateRuntimes**) `artifacts/publish/OmniSharp/{runtime}/{framework}/`
* Test logs in `artifacts/logs`
* Archived binaries in `artifacts/package` (only if `-archive` used on command line)
