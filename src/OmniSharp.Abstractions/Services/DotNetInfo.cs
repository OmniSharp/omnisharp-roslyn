using System;
using System.Collections.Generic;
using NuGet.Versioning;

namespace OmniSharp.Services
{
    public class DotNetInfo
    {
        public static DotNetInfo Empty { get; } = new DotNetInfo();

        public bool IsEmpty { get; }

        public SemanticVersion Version { get; }
        public string OSName { get; }
        public string OSVersion { get; }
        public string OSPlatform { get; }
        public string RID { get; }
        public string BasePath { get; }

        private DotNetInfo()
        {
            IsEmpty = true;
        }

        private DotNetInfo(string version, string osName, string osVersion, string osPlatform, string rid, string basePath)
        {
            IsEmpty = false;

            Version = SemanticVersion.TryParse(version, out var value)
                ? value
                : null;

            OSName = osName;
            OSVersion = osVersion;
            OSPlatform = osPlatform;
            RID = rid;
            BasePath = basePath;
        }

        public static DotNetInfo Parse(List<string> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return Empty;
            }

            var version = string.Empty;
            var osName = string.Empty;
            var osVersion = string.Empty;
            var osPlatform = string.Empty;
            var rid = string.Empty;
            var basePath = string.Empty;

            foreach (var line in lines)
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex >= 0)
                {
                    var name = line.Substring(0, colonIndex).Trim();
                    var value = line.Substring(colonIndex + 1).Trim();

                    if (name.Equals("Version", StringComparison.OrdinalIgnoreCase))
                    {
                        version = value;
                    }
                    else if (name.Equals("OS Name", StringComparison.OrdinalIgnoreCase))
                    {
                        osName = value;
                    }
                    else if (name.Equals("OS Version", StringComparison.OrdinalIgnoreCase))
                    {
                        osVersion = value;
                    }
                    else if (name.Equals("OS Platform", StringComparison.OrdinalIgnoreCase))
                    {
                        osPlatform = value;
                    }
                    else if (name.Equals("RID", StringComparison.OrdinalIgnoreCase))
                    {
                        rid = value;
                    }
                    else if (name.Equals("Base Path", StringComparison.OrdinalIgnoreCase))
                    {
                        basePath = value;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(version) &&
                string.IsNullOrWhiteSpace(osName) &&
                string.IsNullOrWhiteSpace(osVersion) &&
                string.IsNullOrWhiteSpace(osPlatform) &&
                string.IsNullOrWhiteSpace(rid) &&
                string.IsNullOrWhiteSpace(basePath))
            {
                return Empty;
            }

            return new DotNetInfo(version, osName, osVersion, osPlatform, rid, basePath);
        }
    }
}
