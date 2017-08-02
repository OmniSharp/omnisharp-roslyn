using System;
using System.Diagnostics;

public sealed class Platform
{
    public static Platform Current { get; } = GetCurrentPlatform();

    private readonly string _os;
    private readonly string _architecture;

    public bool IsWindows => _os == "Windows";
    public bool IsMacOS => _os == "MacOS";
    public bool IsLinux => _os == "Linux";

    public bool Is32Bit => _architecture == "x86";
    public bool Is64Bit => _architecture == "x64";

    private Platform(string os, string architecture)
    {
        _os = os;
        _architecture = architecture;
    }

    public override string ToString() => $"{_os} ({_architecture})";

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
            else
            {
                throw new Exception($"Unexpected architecture: {osArch}");
            }
        }

        return new Platform(os, architecture);
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