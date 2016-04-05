using System;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Services
{
    public class OmnisharpEnvironment : IOmnisharpEnvironment
    {
        public OmnisharpEnvironment(string path, int port, int hostPid, LogLevel traceType, TransportType transportType, string[] otherArgs)
        {
            if (System.IO.Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                SolutionFilePath = path;
                Path = System.IO.Path.GetDirectoryName(path);
            }
            else
            {
                Path = path;
            }

            Port = port;
            HostPID = hostPid;
            TraceType = traceType;
            TransportType = transportType;
            OtherArgs = otherArgs;
        }

        public LogLevel TraceType { get; }

        public int Port { get; }

        public int HostPID { get; }

        public string Path { get; }

        public string SolutionFilePath { get; }

        public TransportType TransportType { get; }

        public string[] OtherArgs { get; }
    }
}