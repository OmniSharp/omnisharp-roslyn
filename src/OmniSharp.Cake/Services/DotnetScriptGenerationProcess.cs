#if NET6_0_OR_GREATER
using System;
using System.Diagnostics;
using System.IO;
using Cake.Scripting.Transport.Tcp.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OmniSharp.Cake.Services
{
    internal sealed class DotnetScriptGenerationProcess : IScriptGenerationProcess
    {
        private readonly ILogger _logger;
        private readonly IOmniSharpEnvironment _environment;
        private Process _process;

        public DotnetScriptGenerationProcess(string serverExecutablePath, IOmniSharpEnvironment environment, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger(typeof(DotnetScriptGenerationProcess)) ?? NullLogger.Instance;
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
            var fileName = "dotnet";
            var arguments = $"\"{Path.ChangeExtension(ServerExecutablePath, ".dll")}\"";
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

        public string ServerExecutablePath { get; set; }
    }
}
#endif
