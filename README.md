omnisharp-roslyn
================

[![Mono Build Status](https://travis-ci.org/OmniSharp/omnisharp-roslyn.svg?branch=dev)](https://travis-ci.org/OmniSharp/omnisharp-roslyn)
[![Windows Build status](https://ci.appveyor.com/api/projects/status/dj36uvllv0qmkljr/branch/dev?svg=true)](https://ci.appveyor.com/project/david-driscoll/omnisharp-roslyn/branch/dev)

## Introduction

OmniSharp-Roslyn is a .NET development platform based on [Roslyn](https://github.com/dotnet/roslyn) workspaces. It provides project dependencies and language syntax to various IDE and plugins.

OmniSharp-Roslyn is built with the [.NET Core SDK](https://dot.net/). It targets both __netcoreapp1.1__ and __net46__ targets. The __netcoreapp1.1__ build is self contained, while __net46__ build requires __mono__ (>=4.6.0) if it is run on a platform other than Windows.

In addition, if you need the HTTP interface and you want to run on Linux, you'll also need to make sure that you have [libuv](http://libuv.org) installed.

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

For more details, see [Build](https://github.com/OmniSharp/omnisharp-roslyn/blob/dev/BUILD.md).

### VS Code

Add the following setting to your [User Settings or Workspace Settings](https://code.visualstudio.com/Docs/customization/userandworkspace). 

``` JSON
{
  "omnisharp.path": "<Path to the omnisharp executable>"
}
```

## Help wanted!

We have slack room as well. [Get yourself invited](https://omnisharp.herokuapp.com/): [here](https://omnisharp.herokuapp.com/)

