using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Eventing;
using OmniSharp.Options;
using OmniSharp.Utilities;

namespace OmniSharp.Services
{
    internal class DotNetCliService : IDotNetCliService
    {
        const string DOTNET_CLI_UI_LANGUAGE = nameof(DOTNET_CLI_UI_LANGUAGE);

        private readonly ILogger _logger;
        private readonly IEventEmitter _eventEmitter;
        private readonly ConcurrentDictionary<(string WorkingDirectory, string Arguments), Task> _restoreTasks;
        private readonly SemaphoreSlim _semaphore;

        public string DotNetPath { get; }

        public DotNetCliService(ILoggerFactory loggerFactory, IEventEmitter eventEmitter, IOptions<DotNetCliOptions> dotNetCliOptions, IOmniSharpEnvironment environment)
        {
            _logger = loggerFactory.CreateLogger<DotNetCliService>();
            _eventEmitter = eventEmitter;
            _restoreTasks = new();
            _semaphore = new SemaphoreSlim(Environment.ProcessorCount / 2);

            // Check if any of the provided paths have a dotnet executable.
            string executableExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
            foreach (var path in dotNetCliOptions.Value.GetNormalizedLocationPaths(environment))
            {
                if (File.Exists(Path.Combine(path, $"dotnet{executableExtension}")))
                {
                    // We'll take the first path that has a dotnet executable.
                    DotNetPath = Path.Combine(path, "dotnet");
                    break;
                }
                else
                {
                    _logger.LogInformation($"Provided dotnet CLI path does not contain the dotnet executable: '{path}'.");
                }
            }

            // If we still haven't found a dotnet CLI, check the DOTNET_ROOT environment variable.
            if (DotNetPath is null)
            {
                _logger.LogInformation("Checking the 'DOTNET_ROOT' environment variable to find a .NET SDK");
                string dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                if (!string.IsNullOrEmpty(dotnetRoot) && File.Exists(Path.Combine(dotnetRoot, $"dotnet{executableExtension}")))
                {
                    DotNetPath = Path.Combine(dotnetRoot, "dotnet");
                }
            }

            // If we still haven't found the CLI, use the one on the PATH.
            if (DotNetPath is null)
            {
                _logger.LogInformation("Using the 'dotnet' on the PATH.");
                DotNetPath = "dotnet";
            }

            _logger.LogInformation($"DotNetPath set to {DotNetPath}");
        }

        private static void RemoveMSBuildEnvironmentVariables(IDictionary<string, string> environment)
        {
            // Remove various MSBuild environment variables set by OmniSharp to ensure that
            // the .NET CLI is not launched with the wrong values.
            environment.Remove("MSBUILD_EXE_PATH");
            environment.Remove("MSBuildExtensionsPath");
        }

        public Task RestoreAsync(string workingDirectory, string arguments = null, Action onFailure = null)
        {
            return _restoreTasks.GetOrAdd((workingDirectory, arguments), RestoreAsync, onFailure);
        }

        private Task RestoreAsync((string WorkingDirectory, string Arguments) key, Action onFailure = null)
        {
            return Task.Factory.StartNew(() =>
            {
                var (workingDirectory, arguments) = key;

                _logger.LogInformation($"Begin dotnet restore in '{workingDirectory}'");

                var exitStatus = new ProcessExitStatus(-1);
                _eventEmitter.RestoreStarted(workingDirectory);
                _semaphore.Wait();
                try
                {
                    // A successful restore will update the project lock file which is monitored
                    // by the dotnet project system which eventually update the Roslyn model
                    exitStatus = ProcessHelper.Run(DotNetPath, $"restore {arguments}", workingDirectory, updateEnvironment: RemoveMSBuildEnvironmentVariables,
                        outputDataReceived: (data) => _logger.LogDebug(data), errorDataReceived: (data) => _logger.LogDebug(data));
                }
                finally
                {
                    _semaphore.Release();

                    _restoreTasks.TryRemove(key, out _);

                    _eventEmitter.RestoreFinished(workingDirectory, exitStatus.Succeeded);

                    if (exitStatus.Failed && onFailure != null)
                    {
                        onFailure();
                    }

                    _logger.LogInformation($"Finish restoring project {workingDirectory}. Exit code {exitStatus}");
                }
            });
        }

        public Process Start(string arguments, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo(DotNetPath, arguments)
            {
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            RemoveMSBuildEnvironmentVariables(startInfo.Environment);

            return Process.Start(startInfo);
        }

        public DotNetVersion GetVersion(string workingDirectory = null)
        {
            // Ensure that we set the DOTNET_CLI_UI_LANGUAGE environment variable to "en-US" before
            // running 'dotnet --version'. Otherwise, we may get localized results.
            var originalValue = Environment.GetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE);
            Environment.SetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, "en-US");

            try
            {
                Process process;
                try
                {
                    process = Start("--version", workingDirectory);
                }
                catch
                {
                    return DotNetVersion.FailedToStartError;
                }

                if (process.HasExited)
                {
                    return DotNetVersion.FailedToStartError;
                }

                var lines = new List<string>();
                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        lines.Add(e.Data);
                    }
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        lines.Add(e.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                return DotNetVersion.Parse(lines);
            }
            finally
            {
                Environment.SetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, originalValue);
            }
        }

        public DotNetInfo GetInfo(string workingDirectory = null)
        {
            // Ensure that we set the DOTNET_CLI_UI_LANGUAGE environment variable to "en-US" before
            // running 'dotnet --info'. Otherwise, we may get localized results.
            var originalValue = Environment.GetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE);
            Environment.SetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, "en-US");

            try
            {
                Process process;
                try
                {
                    process = Start("--info", workingDirectory);
                }
                catch
                {
                    return DotNetInfo.Empty;
                }

                if (process.HasExited)
                {
                    return DotNetInfo.Empty;
                }

                var lines = new List<string>();
                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        lines.Add(e.Data);
                    }
                };

                process.BeginOutputReadLine();

                process.WaitForExit();

                return DotNetInfo.Parse(lines);
            }
            finally
            {
                Environment.SetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, originalValue);
            }
        }

        /// <summary>
        /// Checks to see if this is a "legacy" .NET CLI. If true, this .NET CLI supports project.json
        /// development; otherwise, it supports .csproj development.
        /// </summary>
        public bool IsLegacy(string workingDirectory = null)
        {
            var version = GetVersion(workingDirectory);

            return IsLegacy(version);
        }

        /// <summary>
        /// Determines whether the specified version is from a "legacy" .NET CLI.
        /// If true, this .NET CLI supports project.json development; otherwise, it supports .csproj development.
        /// </summary>
        public bool IsLegacy(DotNetVersion dotnetVersion)
        {
            if (dotnetVersion.HasError)
            {
                return false;
            }

            var version = dotnetVersion.Version;

            if (version.Major < 1)
            {
                return true;
            }

            if (version.Major == 1 &&
                version.Minor == 0 &&
                version.Patch == 0)
            {
                if (version.PreReleaseLabel.StartsWith("preview1") ||
                    version.PreReleaseLabel.StartsWith("preview2"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
