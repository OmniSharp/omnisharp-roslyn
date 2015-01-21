using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Services
{
    public interface IOmnisharpEnvironment
    {
        LogLevel TraceType { get; }
        int Port { get; }
        string Path { get; }
        string SolutionFilePath { get; }
    }
}