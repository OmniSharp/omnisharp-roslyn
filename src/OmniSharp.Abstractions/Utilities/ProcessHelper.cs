using System;
using System.Diagnostics;

namespace OmniSharp.Utilities
{
    public static class ProcessHelper
    {
        public static string RunAndCaptureOutput(string fileName, string arguments, string workingDirectory = null)
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory ?? string.Empty,
            };

            var process = new Process();
            process.StartInfo = startInfo;

            try
            {
                process.Start();
            }
            catch
            {
                Console.WriteLine($"Failed to launch '{fileName}' with args, '{arguments}'");
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Trim();
        }
    }
}
