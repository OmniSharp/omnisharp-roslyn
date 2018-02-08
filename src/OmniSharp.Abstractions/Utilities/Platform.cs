using System;
using System.IO;

namespace OmniSharp.Utilities
{
    public enum OperatingSystem
    {
        Unknown,
        Windows,
        MacOS,
        Linux
    }

    public enum Architecture
    {
        Unknown,
        x86,
        x64
    }

    public sealed class Platform
    {
        public static Platform Current { get; } = GetCurrentPlatform();

        public OperatingSystem OperatingSystem { get; }
        public Architecture Architecture { get; }
        public Version Version { get; }
        public string LinuxDistributionName { get; }

        private Platform(
            OperatingSystem os = OperatingSystem.Unknown,
            Architecture architecture = Architecture.Unknown,
            Version version = null,
            string linuxDistributionName = null)
        {
            OperatingSystem = os;
            Architecture = architecture;
            Version = version ?? new Version(0, 0);
            LinuxDistributionName = linuxDistributionName ?? string.Empty;
        }

        public override string ToString()
            => !string.IsNullOrEmpty(LinuxDistributionName)
                ? $"{LinuxDistributionName} {Version} ({Architecture})"
                : $"{OperatingSystem} {Version} ({Architecture})";

        private static Platform GetCurrentPlatform()
        {
            var os = OperatingSystem.Unknown;
            var architecture = Architecture.Unknown;

            // Simple check to see if this is Windows. Note: this check is derived from the fact that the
            // System.PlatformID enum has six values (https://msdn.microsoft.com/en-us/library/3a8hyw88.aspx)
            //
            //   * Win32 = 0
            //   * Win32Windows = 1
            //   * Win32NT = 2
            //   * WinCE = 3
            //   * Unix = 4
            //   * Xbox = 5
            //   * MacOSX = 6
            //
            // Essentially, we check to see if this is one of the "windows" values or Xbox. The other values
            // can be a little unreliable, so we'll shell out to 'uname' for Linux and macOS.

            var platformId = (int)Environment.OSVersion.Platform;
            if (platformId <= 3 || platformId == 5)
            {
                os = OperatingSystem.Windows;

                if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86" &&
                    Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
                {
                    architecture = Architecture.x86;
                }
                else
                {
                    architecture = Architecture.x64;
                }
            }
            else
            {
                // If this is not Windows, run 'uname' on Bash to get the OS name and architecture.
                var output = RunOnBashAndCaptureOutput("uname", "-s -m");
                if (string.IsNullOrEmpty(output))
                {
                    return new Platform();
                }

                var values = output.Split(' ');
                var osName = values[0].Trim();
                var osArch = values[1].Trim();

                os = osName.Equals("Darwin", StringComparison.OrdinalIgnoreCase)
                    ? OperatingSystem.MacOS
                    : OperatingSystem.Linux;

                if (osArch.Equals("x86", StringComparison.OrdinalIgnoreCase))
                {
                    architecture = Architecture.x86;
                }
                else if (osArch.Equals("x86_64", StringComparison.OrdinalIgnoreCase))
                {
                    architecture = Architecture.x64;
                }
                else
                {
                    architecture = Architecture.Unknown;
                }
            }

            switch (os)
            {
                case OperatingSystem.Windows:
                    return new Platform(os, architecture, Environment.OSVersion.Version);
                case OperatingSystem.MacOS:
                    return new Platform(os, architecture, GetMacOSVersion());
                case OperatingSystem.Linux:
                    ReadDistroNameAndVersion(out var distroName, out var version);
                    return new Platform(os, architecture, version, distroName);

                default:
                    throw new NotSupportedException("Could not detect the current platform.");
            }
        }

        private static Version GetMacOSVersion()
        {
            var versionText = RunOnBashAndCaptureOutput("sw_vers", "-productVersion");
            return ParseVersion(versionText);
        }

        private static void ReadDistroNameAndVersion(out string distroName, out Version version)
        {
            distroName = null;
            version = null;

            // Details: https://www.freedesktop.org/software/systemd/man/os-release.html
            const string OS_Release_Path = "/etc/os-release";

            if (!File.Exists(OS_Release_Path))
            {
                return;
            }

            var lines = File.ReadAllLines(OS_Release_Path);

            foreach (var line in lines)
            {
                var equalsIndex = line.IndexOf('=');
                if (equalsIndex >= 0)
                {
                    var key = line.Substring(0, equalsIndex).Trim();
                    var value = line.Substring(equalsIndex + 1).Trim();
                    value = value.Trim('"');

                    if (key == "ID")
                    {
                        distroName = value;
                    }
                    else if (key == "VERSION_ID")
                    {
                        version = ParseVersion(value);
                    }

                    if (distroName != null && version != null)
                    {
                        break;
                    }
                }
            }

            if (distroName == null)
            {
                distroName = "Unknown";
            }
        }

        private static Version ParseVersion(string versionText)
        {
            if (!versionText.Contains("."))
            {
                versionText += ".0";
            }

            if (Version.TryParse(versionText, out var version))
            {
                return version;
            }

            return null;
        }

        private static string RunOnBashAndCaptureOutput(string fileName, string arguments)
            => ProcessHelper.RunAndCaptureOutput("/bin/bash", $"-c '{fileName} {arguments}'");
    }
}
