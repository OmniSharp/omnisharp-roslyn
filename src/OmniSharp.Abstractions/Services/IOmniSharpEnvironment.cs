using Microsoft.Extensions.Logging;

namespace OmniSharp.Services
{
    public interface IOmniSharpEnvironment
    {
        LogLevel TraceType { get; }
        int Port { get; }
        int HostPID { get; }
        string Path { get; }
        string SolutionFilePath { get; }
        string SharedPath { get; }
        TransportType TransportType { get; }
        string[] OtherArgs { get; }
    }
}
