using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Services
{
    public class OmnisharpEnvironment : IOmnisharpEnvironment
    {
        public OmnisharpEnvironment(string path, int port, int hostPid, LogLevel traceType, TransportType transportType)
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
        }

        public LogLevel TraceType { get; }

        public int Port { get; }

        public int HostPID { get; }

        public string Path { get; }

        public string SolutionFilePath { get; }

        public TransportType TransportType { get; }
    }
}