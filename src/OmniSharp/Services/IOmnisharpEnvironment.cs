using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Services
{
    public interface IOmnisharpEnvironment
    {
        TraceType TraceType { get; }
        int Port { get; }
        string Path { get; }
        string SolutionFilePath { get; }
    }
}