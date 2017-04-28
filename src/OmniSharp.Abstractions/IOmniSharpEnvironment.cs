using Microsoft.Extensions.Logging;

namespace OmniSharp
{
    public interface IOmniSharpEnvironment
    {
        LogLevel LogLevel { get; }
        int Port { get; }
        int HostProcessId { get; }
        string TargetDirectory { get; }
        string SolutionFilePath { get; }
        string SharedDirectory { get; }
        TransportType TransportType { get; }
        string[] AdditionalArguments { get; }
    }
}
