using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Cake.Scripting.Transport.Tcp.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OmniSharp.Cake.Services
{
    internal sealed class MonoScriptGenerationProcess : IScriptGenerationProcess
    {
        private readonly ILogger _logger;
        private readonly IOmniSharpEnvironment _environment;
        private Process _process;

        public MonoScriptGenerationProcess(string serverExecutablePath, IOmniSharpEnvironment environment, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger(typeof(MonoScriptGenerationProcess)) ?? NullLogger.Instance;
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            ServerExecutablePath = serverExecutablePath;
        }

        public void Dispose()
        {
            _process?.Kill();
            _process?.WaitForExit();
            _process?.Dispose();
        }

        public void Start(int port, string workingDirectory)
        {
            var (fileName, arguments) = GetMonoRuntime();

            if (fileName == null)
            {
                // Something went wrong figurint out mono runtime,
                // try executing exe and let mono handle it.
                fileName = ServerExecutablePath;
            }
            else
            {
                // Else set exe as argument
                arguments += $"\"{ServerExecutablePath}\"";
            }

            arguments += $" --port={port}";
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                arguments += " --verbose";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };

            _logger.LogDebug("Starting \"{fileName}\" with arguments \"{arguments}\"", startInfo.FileName, startInfo.Arguments);
            _process = Process.Start(startInfo);
            _process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    _logger.LogError(e.Data);
                }
            };
            _process.BeginErrorReadLine();
            _process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    _logger.LogDebug(e.Data);
                }
            };
            _process.BeginOutputReadLine();
        }

        private (string, string) GetMonoRuntime()
        {
            // Check using ps how process was started.
            var startInfo = new ProcessStartInfo
            {
                FileName = "sh",
                Arguments = $"-c \"ps -fp {Process.GetCurrentProcess().Id} | tail -n1 | awk '{{print $8}}'\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            var process = Process.Start(startInfo);
            var runtime = process.StandardOutput.ReadToEnd().TrimEnd('\n');
            process.WaitForExit();

            // If OmniSharp bundled Mono runtime, use bootstrap script.
            var script = Path.Combine(Path.GetDirectoryName(runtime), "../run");
            if (File.Exists(script))
            {
                return (script, "--no-omnisharp ");
            }

            // Else use mono directly.
            return (runtime, string.Empty);
        }

        public string ServerExecutablePath { get; set; }
    }
}
