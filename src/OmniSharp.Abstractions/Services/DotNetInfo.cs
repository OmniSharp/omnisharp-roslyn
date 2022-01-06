using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
        public SemanticVersion SdkVersion { get; }
        public string SdksPath { get; }

        private DotNetInfo()
        {
            IsEmpty = true;
        }

        private DotNetInfo(string version, string osName, string osVersion, string osPlatform, string rid, string basePath, string sdkVersion, string sdksPath)
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

            SdkVersion = SemanticVersion.TryParse(sdkVersion, out var sdkValue)
                ? sdkValue
                : null;
            SdksPath = sdksPath;
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
            var sdkVersion = string.Empty;
            var sdksPath = string.Empty;

            foreach (var line in lines)
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex >= 0)
                {
                    var name = line.Substring(0, colonIndex).Trim();
                    var value = line.Substring(colonIndex + 1).Trim();

                    if (string.IsNullOrEmpty(version) && name.Equals("Version", StringComparison.OrdinalIgnoreCase))
                    {
                        version = value;
                    }
                    else if (string.IsNullOrEmpty(osName) && name.Equals("OS Name", StringComparison.OrdinalIgnoreCase))
                    {
                        osName = value;
                    }
                    else if (string.IsNullOrEmpty(osVersion) && name.Equals("OS Version", StringComparison.OrdinalIgnoreCase))
                    {
                        osVersion = value;
                    }
                    else if (string.IsNullOrEmpty(osPlatform) && name.Equals("OS Platform", StringComparison.OrdinalIgnoreCase))
                    {
                        osPlatform = value;
                    }
                    else if (string.IsNullOrEmpty(rid) && name.Equals("RID", StringComparison.OrdinalIgnoreCase))
                    {
                        rid = value;
                    }
                    else if (string.IsNullOrEmpty(basePath) && name.Equals("Base Path", StringComparison.OrdinalIgnoreCase))
                    {
                        basePath = value;
                    }
                }
                else if (string.IsNullOrEmpty(sdkVersion))
                {
                    var getSdkVersionAndPath = new Regex(@"^\s*(\d+\.\d+\.\d+)\s\[(.*)\]\s*$", RegexOptions.Multiline);
                    var match = getSdkVersionAndPath.Match(line);

                    if (match.Success)
                    {
                        sdkVersion = match.Groups[1].Value;
                        sdksPath = match.Groups[2].Value;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(version) &&
                string.IsNullOrWhiteSpace(osName) &&
                string.IsNullOrWhiteSpace(osVersion) &&
                string.IsNullOrWhiteSpace(osPlatform) &&
                string.IsNullOrWhiteSpace(rid) &&
                string.IsNullOrWhiteSpace(basePath) &&
                string.IsNullOrWhiteSpace(sdkVersion) &&
                string.IsNullOrWhiteSpace(sdksPath))
            {
                return Empty;
            }

            return new DotNetInfo(version, osName, osVersion, osPlatform, rid, basePath, sdkVersion, sdksPath);
        }
    }
}
