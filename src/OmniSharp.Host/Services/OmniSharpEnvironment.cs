using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Services
{
    public class OmniSharpEnvironment : IOmniSharpEnvironment
    {
        public string TargetDirectory { get; }
        public string SharedDirectory { get; }

        public string SolutionFilePath { get; }

        public int Port { get; }
        public int HostProcessId { get; }
        public LogLevel LogLevel { get; }
        public TransportType TransportType { get; }

        public string[] AdditionalArguments { get; }

        public OmniSharpEnvironment(
            string path = null,
            int port = -1,
            int hostPid = -1,
            LogLevel traceType = LogLevel.None,
            TransportType transportType = TransportType.Stdio,
            string[] additionalArguments = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                TargetDirectory = Directory.GetCurrentDirectory();
            }
            else if (Directory.Exists(path))
            {
                TargetDirectory = path;
            }
            else if (File.Exists(path) && Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                SolutionFilePath = path;
                TargetDirectory = Path.GetDirectoryName(path);
            }

            if (TargetDirectory == null)
            {
                throw new ArgumentException("OmniSharp only supports being launched with a directory path or a path to a solution (.sln) file.", nameof(path));
            }

            Port = port;
            HostProcessId = hostPid;
            LogLevel = traceType;
            TransportType = transportType;
            AdditionalArguments = additionalArguments;

            // On Windows: %USERPROFILE%\.omnisharp\omnisharp.json
            // On Mac/Linux: ~/.omnisharp/omnisharp.json
            var root =
                Environment.GetEnvironmentVariable("USERPROFILE") ??
                Environment.GetEnvironmentVariable("HOME");

            if (root != null)
            {
                SharedDirectory = Path.Combine(root, ".omnisharp");
            }
        }

        public static bool IsValidPath(string path)
        {
            return string.IsNullOrEmpty(path)
                || Directory.Exists(path)
                || (File.Exists(path) && Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase));
        }
    }
}