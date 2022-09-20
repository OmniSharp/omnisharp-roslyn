using System;
using System.Collections.Generic;

namespace OmniSharp.Services
{
    public class DotNetVersion
    {
        public static DotNetVersion FailedToStartError { get; } = new DotNetVersion("`dotnet --version` failed to start.");

        public bool HasError { get; }
        public string ErrorMessage { get; }

        public SemanticVersion Version { get; }

        private DotNetVersion(SemanticVersion version)
        {
            Version = version;
        }

        private DotNetVersion(string errorMessage)
        {
            HasError = true;
            ErrorMessage = errorMessage;
        }

        public static DotNetVersion Parse(List<string> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return new DotNetVersion("`dotnet --version` produced no output.");
            }

            if (SemanticVersion.TryParse(lines[0], out var version))
            {
                return new DotNetVersion(version);
            }

            var requestedSdkVersion = string.Empty;
            var globalJsonFile = string.Empty;

            foreach (var line in lines)
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex >= 0)
                {
                    var name = line.Substring(0, colonIndex).Trim();
                    var value = line.Substring(colonIndex + 1).Trim();

                    if (string.IsNullOrEmpty(requestedSdkVersion) && name.Equals("Requested SDK version", StringComparison.OrdinalIgnoreCase))
                    {
                        requestedSdkVersion = value;
                    }
                    else if (string.IsNullOrEmpty(globalJsonFile) && name.Equals("global.json file", StringComparison.OrdinalIgnoreCase))
                    {
                        globalJsonFile = value;
                    }
                }
            }

            return requestedSdkVersion.Length > 0 && globalJsonFile.Length > 0
                ? new DotNetVersion($"Install the [{requestedSdkVersion}] .NET SDK or update [{globalJsonFile}] to match an installed SDK.")
                : new DotNetVersion($"Unexpected output from `dotnet --version`: {string.Join(Environment.NewLine, lines)}");
        }
    }
}
