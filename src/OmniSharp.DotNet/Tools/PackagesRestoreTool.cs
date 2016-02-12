using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using OmniSharp.Services;

namespace OmniSharp.DotNet.Tools
{
    public class PackagesRestoreTool
    {
        private readonly ILogger _logger;
        private readonly IEventEmitter _emitter;
        private readonly ConcurrentDictionary<string, object> _projectLocks;
        private readonly SemaphoreSlim _semaphore;

        public PackagesRestoreTool(ILoggerFactory logger, IEventEmitter emitter)
        {
            _logger = logger.CreateLogger<PackagesRestoreTool>();
            _emitter = emitter;

            _projectLocks = new ConcurrentDictionary<string, object>();
            _semaphore = new SemaphoreSlim(Environment.ProcessorCount / 2);
        }

        public void Restore(string projectPath, Action onFailure)
        {
            Task.Factory.StartNew(() =>
            {
                _logger.LogInformation($"Begin restoring project {projectPath}");

                var projectLock = _projectLocks.GetOrAdd(projectPath, new object());
                lock (projectLock)
                {
                    var exitCode = -1;
                    _emitter.RestoreStarted(projectPath);
                    _semaphore.Wait();
                    try
                    {
                        // A successful restore will update the project lock file which is monitored
                        // by the dotnet project system which eventually update the Roslyn model
                        exitCode = RunRestoreProcess(projectPath);
                    }
                    finally
                    {
                        _semaphore.Release();

                        object removedLock;
                        _projectLocks.TryRemove(projectPath, out removedLock);

                        _emitter.RestoreFinished(projectPath, exitCode == 0);

                        if (exitCode != 0)
                        {
                            onFailure();
                        }

                        _logger.LogInformation($"Finish restoring project {projectPath}. Exit code {exitCode}");
                    }
                }
            });
        }

        private int RunRestoreProcess(string projectPath)
        {
            var startInfo = new ProcessStartInfo("dotnet", "restore")
            {
                WorkingDirectory = projectPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var restoreProcess = Process.Start(startInfo);
            if (restoreProcess.HasExited)
            {
                return restoreProcess.ExitCode;
            }

            var lastSignal = DateTime.UtcNow;
            var watchDog = Task.Factory.StartNew(async () =>
            {
                var delay = TimeSpan.FromSeconds(10);
                var timeout = TimeSpan.FromSeconds(60);
                while (!restoreProcess.HasExited)
                {
                    if (DateTime.UtcNow - lastSignal > timeout)
                    {
                        restoreProcess.KillAll();
                    }
                    await Task.Delay(delay);
                }
            });

            restoreProcess.OutputDataReceived += (sender, e) => lastSignal = DateTime.UtcNow;
            restoreProcess.ErrorDataReceived += (sender, e) => lastSignal = DateTime.UtcNow;

            restoreProcess.BeginOutputReadLine();
            restoreProcess.BeginErrorReadLine();
            restoreProcess.WaitForExit();

            return restoreProcess.ExitCode;
        }
    }
}
