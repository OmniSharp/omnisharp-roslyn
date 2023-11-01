# omnisharp-roslyn

[![Build Status](https://dev.azure.com/omnisharp/Builds/_apis/build/status/OmniSharp.omnisharp-roslyn?branchName=master)](https://dev.azure.com/omnisharp/Builds/_build/latest?definitionId=2&branchName=master)

## Introduction

OmniSharp is a .NET development platform based on [Roslyn](https://github.com/dotnet/roslyn) workspaces. It provides project dependencies and C# language services to various IDEs and plugins.

OmniSharp is built with the [.NET Core SDK](https://dot.net/) on Windows and [Mono](http://www.mono-project.com/) on OSX/Linux. It targets both the _net6.0_ and _net472_ target frameworks. The _net6.0_ build requires a .NET SDK version _>=6.0_. When using the _net472_ build on OSX/Linux, _Mono_ version _>=6.4.0_ is required and must be globally installed on the system.

For Arch Linux users, you need package [mono-msbuild](https://archlinux.org/packages/extra/x86_64/mono-msbuild/) (>= 16.3).

In addition, if you need the HTTP interface and you want to run on Linux, you'll also need to make sure that you have [libuv](http://libuv.org) installed. See also https://github.com/OmniSharp/omnisharp-roslyn/issues/1202#issuecomment-421543905 .

## What's new

See our [change log](https://github.com/OmniSharp/omnisharp-roslyn/blob/master/CHANGELOG.md) for all of the updates.

## Using OmniSharp

OmniSharp ships in two flavors:

-   Stdio server
-   HTTP server

### Downloading OmniSharp

When using OmniSharp with an editor extension (e.g. VIM, Emacs, VS Code), the extension will download or bundle OmniSharp automatically. If you wish to download OmniSharp manually though, follow the steps below.

#### Stable releases

Stable releases are published using [GitHub releases](https://github.com/OmniSharp/omnisharp-roslyn/releases). Each release contains a set of binaries for various operating systems and processing architectures.

#### Pre-releases

Pre-release versions are available in Azure Blob Storage, they can be viewed using the following URL `https://roslynomnisharp.blob.core.windows.net/releases?restype=container&comp=list&prefix={version}`, where the `{version}` placeholder can be found in the [changelog](https://github.com/OmniSharp/omnisharp-roslyn/blob/master/CHANGELOG.md). For example, all `1.37.x` versions (including all betas and prereleases such as `1.37.4-beta.5`) can be viewed using `https://roslynomnisharp.blob.core.windows.net/releases?restype=container&comp=list&prefix=1.37`. Please note that the listing is limited to 5000 entries.

Every merge to `master` is automatically published to this feed and individual release is then available using the following URL convention:
`https://roslynomnisharp.blob.core.windows.net/releases/{version}/{packagename}-{os/arch}.{ext}`

-   Version is auto incremented and is visible in the travis or appveyor build output
-   Package Name would be either `omnisharp` or `omnisharp.http`
-   `os/arch` will be one of the following:
    -   `win-x64`
    -   `win-x86`
    -   `win-arm64`
    -   `linux-x64`
    -   `linux-x86`
    -   `linux-musl-x64`
    -   `linux-arm64`
    -   `linux-musl-arm64`
    -   `osx`
    -   `mono` (Requires global mono installed)
-   Extensions are archive specific, windows will be `zip` and all others will be `tar.gz`.

### Building

**On Windows**:

```
> ./build.ps1
```

**On Linux / Unix**:

```
$ ./build.sh
```

You can find the output under `artifacts/publish/OmniSharp/<runtime id>/<target framework>/`.

The executable is either `OmniSharp.exe` or `OmniSharp`.

For more details, see [Build](https://github.com/OmniSharp/omnisharp-roslyn/blob/master/BUILD.md).

### VS Code

Add the following setting to your [User Settings](https://code.visualstudio.com/Docs/customization/userandworkspace).

```JSON
{
  "omnisharp.path": "<Path to the omnisharp executable>"
}
```

The above option can also be set to:

-   "latest" - To consume the latest build from the master branch
-   A specific version number like `1.29.2-beta.60`

In order to be able to attach a debugger, add the following setting to your [User or Workspace settings](https://code.visualstudio.com/Docs/customization/userandworkspace):

```JSON
{
  "omnisharp.waitForDebugger": true
}
```

This will print the OmniSharp process ID in the VS Code OmniSharp output panel and pause the start of the server until a debugger is attached to this process. This is equivalent to launching OmniSharp from a command line with the `--debug` flag.

### Configuration

OmniSharp provides a rich set of hierarchical configuration options, controlled via startup arguments, environment variables and `omnisharp.json` file. For more details please visit the [Configuration Options](https://github.com/OmniSharp/omnisharp-roslyn/wiki/Configuration-Options) section of the wiki.

## Help wanted!

We have slack room as well. [Get yourself invited](https://omnisharp.herokuapp.com/): [here](https://omnisharp.herokuapp.com/)

## License

Copyright © .NET Foundation, and contributors.

OmniSharp is provided as-is under the MIT license. For more information see [LICENSE](https://github.com/OmniSharp/omnisharp-roslyn/blob/master/license.md).

## Code of Conduct

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/)
to clarify expected behavior in our community.
For more information see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## Contribution License Agreement

By signing the [CLA](https://cla.dotnetfoundation.org/OmniSharp/omnisharp-roslyn), the community is free to use your contribution to .NET Foundation projects.

## .NET Foundation

This project is supported by the [.NET Foundation](http://www.dotnetfoundation.org).
