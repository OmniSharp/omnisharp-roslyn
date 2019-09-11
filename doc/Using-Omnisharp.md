# Using OmniSharp

OmniSharp is a C# based console application that has a rich API to support code intelligence for the C# language.  With OmniSharp as the server, any text editor that supports plugins can be turned into a C# Development environment.

This document is a rough overview of how to interact with OmniSharp, and what the various features are.

## Interfaces
OmniSharp supports several interfaces over the command line.

1. Http
  The http interface allows you to start up the server and communicate with it over HTTP.  This interface is pull based, so the server has no way to push information to the client.  The client may listen to the `Stdout` stream for logging information from the server.

    You may specify the port for http using the `-p <port>` flag.
2. Stdio (using the flag `--stdio`)
  Stdio uses the standard process interfaces.  The server will read `Stdin` for requests, and pipe responses back out over `Stdout`.  Each line on either interface is a single request / response.  With this interface, the server can have a two-way relationship with the server, such that the server can intelligently provide information for the client to consume.  This can be anything from Diagnostics, to new references, package restores, and so on.

        OmniSharp.exe --stdio

## Solutions
When starting the server you must specify a solution file, or a directory where OmniSharp will find a solution.

    OmniSharp.exe -s <solutionPath>

The server will detect the project and start up the project systems for the types of projects it finds.

## Verbose logging
You can turn on verbose logging with the `-v` switch.

## Auto-shutdown
OmniSharp supports the ability to shut it self down in the event its host process dies.  If for example it crashed, or the user force killed the process.

    OmniSharp.exe --hostPID

## One-Based Indexes
For historical reasons OmniSharp defaults to using one-based indices.   That means that the incoming requests assume that the first line of a file is `1` and the first column of a line is also `1`.  A feature has been added for editors that default to zero-based indices, using this feature OmniSharp will automatically translate the indices to zero-based for you automatically.  This is helpful when dealing with deserializing lots of objects on the Editor side.

    OmniSharp.exe --zero-based-indices

## Plugins
TODO: Plugins will be supported in the future
