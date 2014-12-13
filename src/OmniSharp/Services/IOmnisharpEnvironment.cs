using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Services
{
    public interface IOmnisharpEnvironment
    {
        TraceType TraceType { get; }
        string Path { get; }
        string SolutionFilePath { get; }
    }
}