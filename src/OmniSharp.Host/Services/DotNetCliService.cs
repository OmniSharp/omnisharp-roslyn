using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using OmniSharp.Eventing;
using OmniSharp.Utilities;

namespace OmniSharp.Services
{
    internal class DotNetCliService : IDotNetCliService
    {
        private readonly ILogger _logger;
        private readonly IEventEmitter _eventEmitter;
        private readonly ConcurrentDictionary<string, object> _locks;
        private readonly SemaphoreSlim _semaphore;

        public string DotNetPath { get; }

        public DotNetCliService(ILoggerFactory loggerFactory, IEventEmitter eventEmitter, string dotnetPath = null)
        {
            _logger = loggerFactory.CreateLogger<DotNetCliService>();
            _eventEmitter = eventEmitter;
            _locks = new ConcurrentDictionary<string, object>();
            _semaphore = new SemaphoreSlim(Environment.ProcessorCount / 2);

            DotNetPath = dotnetPath ?? "dotnet";

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
            return Task.Factory.StartNew(() =>
            {
                _logger.LogInformation($"Begin dotnet restore in '{workingDirectory}'");

                var restoreLock = _locks.GetOrAdd(workingDirectory, new object());
                lock (restoreLock)
                {
                    var exitStatus = new ProcessExitStatus(-1);
                    _eventEmitter.RestoreStarted(workingDirectory);
                    _semaphore.Wait();
                    try
                    {
                        // A successful restore will update the project lock file which is monitored
                        // by the dotnet project system which eventually update the Roslyn model
                        exitStatus = ProcessHelper.Run(DotNetPath, $"restore {arguments}", workingDirectory, updateEnvironment: RemoveMSBuildEnvironmentVariables);
                    }
                    finally
                    {
                        _semaphore.Release();

                        _locks.TryRemove(workingDirectory, out _);

                        _eventEmitter.RestoreFinished(workingDirectory, exitStatus.Succeeded);

                        if (exitStatus.Failed && onFailure != null)
                        {
                            onFailure();
                        }

                        _logger.LogInformation($"Finish restoring project {workingDirectory}. Exit code {exitStatus}");
                    }
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

        public SemanticVersion GetVersion(string workingDirectory = null)
        {
            var output = ProcessHelper.RunAndCaptureOutput(DotNetPath, "--version", workingDirectory);

            return SemanticVersion.Parse(output);
        }

        public DotNetInfo GetInfo(string workingDirectory = null)
        {
            const string DOTNET_CLI_UI_LANGUAGE = nameof(DOTNET_CLI_UI_LANGUAGE);

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
        public bool IsLegacy(SemanticVersion version)
        {
            if (version.Major < 1)
            {
                return true;
            }

            if (version.Major == 1 &&
                version.Minor == 0 &&
                version.Patch == 0)
            {
                if (version.Release.StartsWith("preview1") ||
                    version.Release.StartsWith("preview2"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
