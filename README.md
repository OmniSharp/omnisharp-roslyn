# omnisharp-roslyn

## Introduction

OmniSharp is a .NET development platform based on [Roslyn](https://github.com/dotnet/roslyn) workspaces. It provides project dependencies and C# language services to various IDEs and plugins.

OmniSharp is built with the [.NET SDK](https://dotnet.microsoft.com/download).

## What's new

See our [change log](https://github.com/OmniSharp/omnisharp-roslyn/blob/master/CHANGELOG.md) for all of the updates.

## Using OmniSharp

OmniSharp ships in two flavors:

-   Stdio server
-   HTTP server

### Prerequisites

-   [.NET 11.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)

### Downloading OmniSharp

When using OmniSharp with an editor extension (for example, Vim or Emacs), the extension will download or bundle OmniSharp automatically. To download OmniSharp manually, follow the steps below.

#### Releases

Stable and pre-release versions are published using [GitHub releases](https://github.com/OmniSharp/omnisharp-roslyn/releases). Each release contains binaries for the supported operating systems and processor architectures.

### Building

**On Windows:**

```powershell
./build.ps1
```

**On Linux or macOS:**

```bash
./build.sh
```

You can find the output under `artifacts/publish/OmniSharp/<runtime id>/`.

The executable is either `OmniSharp.exe` or `OmniSharp`.

For more details, see [Build](https://github.com/OmniSharp/omnisharp-roslyn/blob/master/BUILD.md).

### Configuration

OmniSharp provides a rich set of hierarchical configuration options, controlled via startup arguments, environment variables, and the `omnisharp.json` file. For more details, see the [Configuration Options](https://github.com/OmniSharp/omnisharp-roslyn/wiki/Configuration-Options) wiki page.

## License

Copyright © .NET Foundation, and contributors.

OmniSharp is provided as-is under the MIT license. For more information, see [LICENSE](license.md).

## Code of Conduct

This project has adopted the [Code of Conduct](CODE_OF_CONDUCT.md) to clarify expected behavior in our community.

## Contribution License Agreement

By signing the [CLA](https://cla.dotnetfoundation.org/OmniSharp/omnisharp-roslyn), the community is free to use your contribution to .NET Foundation projects.

## .NET Foundation

This project is supported by the [.NET Foundation](https://dotnetfoundation.org/).
