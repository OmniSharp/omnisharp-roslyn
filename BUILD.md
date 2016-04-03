# Usage

Run `build.(ps1|sh)` with the desired set of arguments (see below for options).
The build script itself is `build.cake`, written in C# using the Cake build automation system.
All build related activites should be encapsulated in this file for cross-platform access.

# Arguments

## Primary

  `-target=TargetName`: The name of the build task/target to execute (see below for listing and details).
    Defaults to `Default`.

  `-configuration=(Release|Debug)`: The configuration to build.
    Defaults to `Release`.

## Extra

  `-test-configuration=(Release|Debug)`: The configuration to use for the unit tests.
    Defaults to `Debug`.
  
  `-install-path=Path`: Path used for the **Install** target.
    Defaults to `(%USERPROFILE%|$HOME)/.omnisharp/local`
  
  `-archive`: Enable the generation of publishable archives after a build.

# Targets

**Default**: Alias for Local.

**Local**: Full build including testing for the machine-local runtime.

**All**: Same as local, but targeting all runtimes selected by `PopulateRuntimes` in `build.cake`.
  Currently configured to also build for a 32-bit Windows runtime on Windows machines.
  No additional runtimes are currently selected on non-Windows machines.

**Quick**: Local build which skips all testing.

**Install**: Same as quick, but installs the generated binaries into `install-path`.

**SetPackageVersions**: Updates the dependency versions found within `project.json` files using information from `depversion.json`.
  Used for maintainence within the project, not needed for end-users. More information below.

# Configuration files

## build.json

A number of build-related options, including folder names for different entities. Interesting options:

**DotNetInstallScriptURL**: The URL where the .NET SDK install script is located.
  Can be used to pin to a specific script version, if a breaking change occurs.
  
**"DotNetChannel"**: The .NET SDK channel used for retreiving the tools.

**"DotNetVersion"**: The .NET SDK version used for the build. Can be used to pin to a specific version.
  Using the string `Latest` will retrieve the latest version.

## depversion.json

A listing of all dependencies (and their desired versions) used by `project.json` files throughout the project.
Allows for quick and automatic updates to the dependency version numbers using the **SetPackageVersions** target.

# Artifacts generated

* Binaries of OmniSharp and its libraries built for the local machine in `artifacts/publish/OmniSharp/default/{framework}/`
* Scripts to run OmniSharp at `scripts/OmniSharp(.Core)(.cmd)`
  * These scripts are updated for every build and every install.
  * The scripts point to the installed binary after and install, otherwise just the build folder (reset if a new build occurs without an install).
* Binaries of OmniSharp and its libraries cross-compiled for other runtimes (if selected in **PopulateRuntimes**) `artifacts/publish/OmniSharp/{runtime}/{framework}/`
* Test logs in `artifacts/logs`
* Archived binaries in `artifacts/package` (only if `-archive` used on command line)

# Requirements

The build system requires Mono to be installed on non-Windows machines as Cake is not built using .NET Core (yet).
