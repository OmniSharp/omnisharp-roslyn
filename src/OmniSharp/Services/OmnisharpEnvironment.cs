using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Services
{
    public class OmnisharpEnvironment : IOmnisharpEnvironment
    {
        public OmnisharpEnvironment(string path, int port, int hostPid, LogLevel traceType, TransportType transportType, bool enablePackageRestore, int packageRestoreTimeout)
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
            ConfigurationPath = System.IO.Path.Combine(Path, "omnisharp.json");
            EnablePackageRestore = enablePackageRestore;
            PackageRestoreTimeout = packageRestoreTimeout;
        }

        public LogLevel TraceType { get; }

        public int Port { get; }

        public int HostPID { get; }

        public string Path { get; }

        public string SolutionFilePath { get; }
        
        public string ConfigurationPath { get; }

        public TransportType TransportType { get; }
        public bool EnablePackageRestore { get; }
        public int PackageRestoreTimeout { get; }
    }
}