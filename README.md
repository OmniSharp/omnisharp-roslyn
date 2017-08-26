omnisharp-roslyn
================

[![Mono Build Status](https://travis-ci.org/OmniSharp/omnisharp-roslyn.svg?branch=dev)](https://travis-ci.org/OmniSharp/omnisharp-roslyn)
[![Windows Build status](https://ci.appveyor.com/api/projects/status/dj36uvllv0qmkljr/branch/dev?svg=true)](https://ci.appveyor.com/project/david-driscoll/omnisharp-roslyn/branch/dev)

## Introduction

OmniSharp-Roslyn is a .NET development platform based on [Roslyn](https://github.com/dotnet/roslyn) workspaces. It provides project dependencies and language syntax to various IDE and plugins.

OmniSharp-Roslyn is built with the [.NET Core SDK](https://dot.net/) on Windows and [Mono](http://www.mono-project.com/) on OSX/Linux. It targets the __net46__ target framework. OmniSharp requires __mono__ (>=5.2.0) if it is run on a platform other than Windows.

In addition, if you need the HTTP interface and you want to run on Linux, you'll also need to make sure that you have [libuv](http://libuv.org) installed.

## What's new

See our [change log](https://github.com/OmniSharp/omnisharp-roslyn/blob/master/CHANGELOG.md) for all of the updates.

## Using the latest OmniSharp-Roslyn with VS Code

### Building

**On Windows**:

```
> git checkout dev
> ./build.ps1
```

**On Linux / Unix**:

```
$ git checkout dev
$ ./build.sh
```

You can find the output under `artifacts/publish/OmniSharp/<runtime id>/<target framework>/`.

The executable is either `OmniSharp.exe` or `OmniSharp`.

For more details, see [Build](https://github.com/OmniSharp/omnisharp-roslyn/blob/master/BUILD.md).

### VS Code

Add the following setting to your [User Settings or Workspace Settings](https://code.visualstudio.com/Docs/customization/userandworkspace). 

``` JSON
{
  "omnisharp.path": "<Path to the omnisharp executable>"
}
```

In order to be able to attach a debugger, add the following setting:

```JSON
{
  "omnisharp.waitForDebugger": true
}
```

This will print the OmniSharp process ID in the VS Code OmniSharp output panel and pause the start of the server until a debugger is attached to this process. This is equivalent to launching OmniSharp from a command line with the `--debug` flag.

## Help wanted!

We have slack room as well. [Get yourself invited](https://omnisharp.herokuapp.com/): [here](https://omnisharp.herokuapp.com/)

