using System;
using System.Diagnostics;

public sealed class Platform
{
    public static Platform Current { get; } = GetCurrentPlatform();

    private readonly string _os;
    private readonly string _architecture;

    public Version Version { get; }
    public string DistroName { get; }

    public bool IsWindows => _os == "Windows";
    public bool IsMacOS => _os == "MacOS";
    public bool IsLinux => _os == "Linux";

    public bool IsX86 => _architecture == "x86";
    public bool IsX64 => _architecture == "x64";
    public bool IsArm64 => _architecture == "arm64";

    private Platform(string os, string architecture, Version version, string distroName = null)
    {
        _os = os;
        _architecture = architecture;
        this.Version = version;
        this.DistroName = distroName;
    }

    public override string ToString() => $"{DistroName ?? _os} {Version} ({_architecture})";

    private static Platform GetCurrentPlatform()
    {
        string os, architecture;

        // Simple check to see if this is Windows.
        var platformId = (int)Environment.OSVersion.Platform;
        if (platformId <= 3 || platformId == 5)
        {
            os = "Windows";

            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86" &&
                Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
            {
                architecture = "x86";
            }
            else if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "ARM64" &&
                Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
            {
                architecture = "arm64";
            }
            else
            {
                architecture = "x64";
            }
        }
        else
        {
            // If this is not Windows, run 'uname' to get the OS name and architecture.
            var output = RunAndCaptureOutput("uname", "-s -m");
            var values = output.Split(' ');
            var osName = values[0];
            var osArch = values[1];

            os = osName.Equals("Darwin", StringComparison.OrdinalIgnoreCase)
                ? "MacOS"
                : "Linux";

            if (osArch.Equals("x86", StringComparison.OrdinalIgnoreCase))
            {
                architecture = "x86";
            }
            else if (osArch.Equals("x86_64", StringComparison.OrdinalIgnoreCase))
            {
                architecture = "x64";
            }
            else if (osArch.Equals("aarch64", StringComparison.OrdinalIgnoreCase)
                || osArch.Equals("arm64", StringComparison.OrdinalIgnoreCase))
            {
                architecture = "arm64";
            }
            else
            {
                throw new Exception($"Unexpected architecture: {osArch}");
            }
        }

        switch (os)
        {
            case "Windows":
                return new Platform(os, architecture, Environment.OSVersion.Version);
            case "MacOS":
                var versionText = RunAndCaptureOutput("sw_vers", "-productVersion");
                return new Platform(os, architecture, new Version(versionText));
            case "Linux":
                string distroName;
                Version version;
                ReadDistroNameAndVersion(out distroName, out version);

                return new Platform(os, architecture, version, distroName);
            default:
                throw new ArgumentException(nameof(os));
        }
    }

    private static void ReadDistroNameAndVersion(out string distroName, out Version version)
    {
        distroName = null;
        version = null;

        string OS_Release_Path = "/etc/os-release";

        if (!System.IO.File.Exists(OS_Release_Path))
        {
            OS_Release_Path = "/usr/lib/os-release";
        }

        if (!System.IO.File.Exists(OS_Release_Path))
        {
            return;
        }

        var lines = System.IO.File.ReadAllLines(OS_Release_Path);

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
                    // Normalize any versions to include a Major and Minor version
                    if (!value.Contains("."))
                    {
                        value = value + ".0";
                    }
                    version = new Version(value);
                }
                
                if (distroName != null && version != null)
                {
                    break;
                }
            }
        }
    }

    private static string RunAndCaptureOutput(string fileName, string arguments, string workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? string.Empty,
        };

        try
        {
            var process = Process.Start(startInfo);

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Trim();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to launch '{fileName}' with args, '{arguments}'", ex);
        }
    }
}
