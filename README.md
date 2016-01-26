omnisharp-roslyn
================

[![Mono Build Status](https://travis-ci.org/OmniSharp/omnisharp-roslyn.svg?branch=master)](https://travis-ci.org/OmniSharp/omnisharp-roslyn)
[![Windows Build status](https://ci.appveyor.com/api/projects/status/dj36uvllv0qmkljr?svg=true)](https://ci.appveyor.com/project/david-driscoll/omnisharp-roslyn)

Omnisharp based on [Roslyn](https://github.com/dotnet/roslyn) workspaces

This currently requires dnx version - 1.0.0-beta4 (see https://github.com/aspnet/Home). If you need the HTTP interface and you want to run on linux, you'll also need to make sure that you have libuv installed.

If you are using Mono, you'll need a minimum of verson 4.0.1

Run the server with ```./scripts/Omnisharp -s /path/to/project -p portnumber```

Check out the issue tracker https://huboard.com/OmniSharp/omnisharp-roslyn

## ASPNET RC2 Note
For the latest version you must have one of the specific pinned versions of ASPNET RC2 installed, they are as follows...
* `dnx-clr-win-x86.1.0.0-rc2-16420`
* `dnx-mono.1.0.0-rc2-16420`
* `dnx-coreclr-win-x64.1.0.0-rc2-16420`
* `dnx-coreclr-darwin-x64.1.0.0-rc2-16420`
* `dnx-coreclr-linux-x64.1.0.0-rc2-16420`

## Help wanted!
Visit https://jabbr.net/#/rooms/omnisharp if you'd like to help out.
