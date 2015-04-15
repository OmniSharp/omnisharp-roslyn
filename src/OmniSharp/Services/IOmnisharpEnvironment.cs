using Microsoft.Framework.Logging;

namespace OmniSharp.Services
{
    public interface IOmnisharpEnvironment
    {
        LogLevel TraceType { get; }
        int Port { get; }
        int HostPID { get; }
        string Path { get; }
        string SolutionFilePath { get; }
        string ConfigurationPath { get; } 
        TransportType TransportType { get; }
    }
}