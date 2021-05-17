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
        public int HostProcessId { get; }
        public LogLevel LogLevel { get; }
        public string[] AdditionalArguments { get; }

        public OmniSharpEnvironment(
            string path = null,
            int hostPid = -1,
            LogLevel logLevel = LogLevel.None,
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
            else if (File.Exists(path) && (Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".slnf", StringComparison.OrdinalIgnoreCase)))
            {
                SolutionFilePath = path;
                TargetDirectory = Path.GetDirectoryName(path);
            }

            if (TargetDirectory == null)
            {
                throw new ArgumentException("OmniSharp only supports being launched with a directory path or a path to a solution (.sln, .slnf) file.", nameof(path));
            }

            HostProcessId = hostPid;
            LogLevel = logLevel;
            AdditionalArguments = additionalArguments;

            // First look at OMNISHARPHOME to allow users to set custom location, then
            // On Windows: %USERPROFILE%\.omnisharp\omnisharp.json
            // On Mac/Linux: ~/.omnisharp/omnisharp.json
            var root =
                Environment.GetEnvironmentVariable("OMNISHARPHOME") ??
                Environment.GetEnvironmentVariable("USERPROFILE") ??
                Environment.GetEnvironmentVariable("HOME");

            if (root != null)
            {
                SharedDirectory = Path.Combine(root, ".omnisharp");
            }
        }
    }
}
