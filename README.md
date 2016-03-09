omnisharp-roslyn
================

[![Mono Build Status](https://travis-ci.org/OmniSharp/omnisharp-roslyn.svg?branch=dev)](https://travis-ci.org/OmniSharp/omnisharp-roslyn)
[![Windows Build status](https://ci.appveyor.com/api/projects/status/dj36uvllv0qmkljr/branch/dev?svg=true)](https://ci.appveyor.com/project/david-driscoll/omnisharp-roslyn/branch/dev)

## Introduction

OmniSharp-Roslyn is a .NET development platform based on on [Roslyn](https://github.com/dotnet/roslyn) workspaces. It provides project dependencies and language syntax to various IDE and plugins.

OmniSharp-Roslyn is now built with [dotnet-cli]( http://dotnet.github.io/getting-started/). It targets both __dnxcore50__ and __dnx451__ targets. The __dnxcore50__ build is self contained, while __dnx451__ build requires __mono__ (>4.0.1) if it is ran on platform other than Windows.

In addition if you need the HTTP interface and you want to run on Linux, you'll also need to make sure that you have [libuv](http://libuv.org) installed.

## Use the latest OmniSharp-Roslyn in VS Code

### Build

```
On Windows:
> git checkout dev
> ./build.ps1

On Linux / Unix:
$ git checkout dev
$ ./build.sh

```

You cand find the output under `artifacts/publish/<runtime id>/<target framework>/`.

The executable is `OmniSharp.exe` or `OmniSharp`.

### VS Code

Add following setting to your [User Settings or Workspace Settings](https://code.visualstudio.com/Docs/customization/userandworkspace). 

``` JSON
{
  "csharp.omnisharp": "<Path to the omnisharp executable>"
}
```

### For Linux / Unix

If you're running OmniSharp on Linux or Unix, you need to set `DOTNET_REFERENCE_ASSEMBLIES_PATH` for projects targetting desktop CLR. You can use following bash script to set `DOTNET_REFERENCE_ASSEMBLIES_PATH` to mono's reference assemblies folder.

``` Bash
if [ -z "$DOTNET_REFERENCE_ASSEMBLIES_PATH" ]; then
  if [ $(uname) == Darwin ] && [ -d "/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/xbuild-frameworks" ]; then
    export DOTNET_REFERENCE_ASSEMBLIES_PATH="/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/xbuild-frameworks"
  elif [ -d "/usr/local/lib/mono/xbuild-frameworks" ]; then
    export DOTNET_REFERENCE_ASSEMBLIES_PATH="/usr/local/lib/mono/xbuild-frameworks"
  elif [ -d "/usr/lib/mono/xbuild-frameworks" ]; then
    export DOTNET_REFERENCE_ASSEMBLIES_PATH="/usr/lib/mono/xbuild-frameworks"
  fi
fi
```

## Help wanted!

Visit https://jabbr.net/#/rooms/omnisharp if you'd like to help out.

Please feel free to file issue.
