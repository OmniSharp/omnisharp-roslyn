using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

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

            var process = new Process
            {
                StartInfo = startInfo
            };

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

        public static ProcessExitStatus Run(
            string fileName,
            string arguments,
            string workingDirectory = null,
            Action<string> outputDataReceived = null,
            Action<string> errorDataReceived = null,
            Action<IDictionary<string, string>> updateEnvironment = null)
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory ?? string.Empty,
            };

            updateEnvironment(startInfo.Environment);

            var process = new Process
            {
                StartInfo = startInfo
            };

            try
            {
                process.Start();
            }
            catch
            {
                Console.WriteLine($"Failed to launch '{fileName}' with args, '{arguments}'");
                return new ProcessExitStatus(process.ExitCode, started: false);
            }

            if (process.HasExited)
            {
                return new ProcessExitStatus(process.ExitCode);
            }

            var lastSignal = DateTime.UtcNow;
            var watchDog = Task.Factory.StartNew(async () =>
            {
                var delay = TimeSpan.FromSeconds(10);
                var timeout = TimeSpan.FromSeconds(60);
                while (!process.HasExited)
                {
                    if (DateTime.UtcNow - lastSignal > timeout)
                    {
                        process.KillChildrenAndThis();
                    }

                    await Task.Delay(delay);
                }
            });

            process.OutputDataReceived += (_, e) =>
            {
                lastSignal = DateTime.UtcNow;

                if (outputDataReceived != null && !string.IsNullOrEmpty(e.Data))
                {
                    outputDataReceived(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                lastSignal = DateTime.UtcNow;

                if (errorDataReceived != null && !string.IsNullOrEmpty(e.Data))
                {
                    errorDataReceived(e.Data);
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            return new ProcessExitStatus(process.ExitCode);
        }
    }
}
