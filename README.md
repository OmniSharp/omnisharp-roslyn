omnisharp-roslyn
================

[![Mono Build Status](https://travis-ci.org/OmniSharp/omnisharp-roslyn.svg?branch=master)](https://travis-ci.org/OmniSharp/omnisharp-roslyn)
[![Windows Build status](https://ci.appveyor.com/api/projects/status/dj36uvllv0qmkljr?svg=true)](https://ci.appveyor.com/project/david-driscoll/omnisharp-roslyn)

## Introduction

OmniSharp-Roslyn is a .NET development platform based on on [Roslyn](https://github.com/dotnet/roslyn) workspaces. It provides project dependencies and language syntax to various IDE and plugins.

OmniSharp-Roslyn is now built with [dotnet-cli]( http://dotnet.github.io/getting-started/). It targets both __dnxcore50__ and __dnx451__ targets. The __dnxcore50__ build is self contained, while __dnx451__ build requires __mono__ (>4.0.1) if it is ran on platform other than Windows.

In addition if you need the HTTP interface and you want to run on Linux, you'll also need to make sure that you have [libuv](http://libuv.org) installed.

## Use the latest OmniSharp-Roslyn in VS Code

### Build

```
On Windows:
> git checkout troy/use.dotnet
> ./build.ps1

On Linux / Unix:
$ git checkout troy/use.dotnet
$ ./build.sh

```

You cand find the output under `artifacts/publish/<target framework>/`. The executable is `OmniSharp.exe` or `OmniSharp`.

### VS Code

Add following setting to your User Settings or Workspace Settings. 

_Update path to fit your environment_

``` JSON
{
  "csharp.omnisharp": "C:\\code\\omnisharp-roslyn\\artifacts\\publish\\OmniSharp\\dnx451\\omnisharp.exe"
}
```

### Issues
Check out the issue tracker https://huboard.com/OmniSharp/omnisharp-roslyn

1. As of now, the new DotNet project system works on dnxcore50 target framework only. Pending on this [change](https://github.com/dotnet/cli/commit/c881516abf4ee50ebea4e6d8fd065939248ec9e6) to go into the build.

## Help wanted!
Visit https://jabbr.net/#/rooms/omnisharp if you'd like to help out.
