using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Framework.DesignTimeHost.Models;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Services;

namespace OmniSharp.Dnx
{
    public class PackagesRestoreTool
    {
        private readonly DnxOptions _options;
        //private readonly ILogger _logger;
        private readonly IEventEmitter _emitter;
        private readonly DnxContext _context;
        private readonly DnxPaths _paths;
        private readonly object _lock;
        private readonly IDictionary<string, object> _projectLocks;
        private readonly SemaphoreSlim _semaphore;

        public PackagesRestoreTool(DnxOptions options, IEventEmitter emitter, DnxContext context, DnxPaths paths)
        {
            _options = options;
            //_logger = logger.CreateLogger<PackagesRestoreTool>();
            _emitter = emitter;
            _context = context;
            _paths = paths;
            _lock = new object();
            _projectLocks = new Dictionary<string, object>();
            _semaphore = new SemaphoreSlim(Environment.ProcessorCount / 2);
        }

        public void Run(Project project)
        {
            if (!_options.EnablePackageRestore)
            {
                return;
            }

            Task.Factory.StartNew(() =>
            {
                object projectLock;
                lock (_lock)
                {
                    if (!_projectLocks.TryGetValue(project.Path, out projectLock))
                    {
                        projectLock = new object();
                        _projectLocks.Add(project.Path, projectLock);
                    }
                }

                lock (projectLock)
                {
                    var exitCode = -1;
                    _emitter.Emit(EventTypes.PackageRestoreStarted, new PackageRestoreMessage() { FileName = project.Path });
                    _semaphore.Wait();
                    try
                    {
                        exitCode = DoRun(project, 1);

                        _context.Connection.Post(new Message()
                        {
                            ContextId = project.ContextId,
                            MessageType = "RestoreComplete",
                            HostId = _context.HostId
                        });
                    }
                    finally
                    {
                        _semaphore.Release();
                        _projectLocks.Remove(project.Path);
                        _emitter.Emit(EventTypes.PackageRestoreFinished, new PackageRestoreMessage()
                        {
                            FileName = project.Path,
                            Succeeded = exitCode == 0
                        });
                    }
                }
            });
        }

        private int DoRun(Project project, int retry)
        {
            var psi = new ProcessStartInfo()
            {
                FileName = _paths.Dnu,
                WorkingDirectory = Path.GetDirectoryName(project.Path),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = "restore"
            };

            //_logger.LogInformation("restore packages {0} {1} for {2}", psi.FileName, psi.Arguments, psi.WorkingDirectory);

            var restoreProcess = Process.Start(psi);
            if (restoreProcess.HasExited)
            {
                //_logger.LogError("restore command ({0}) failed with error code {1}", psi.FileName, restoreProcess.ExitCode);
                return restoreProcess.ExitCode;
            }

            // watch dog to workaround dnu restore hangs
            var lastSignal = DateTime.UtcNow;
            var wasKilledByWatchDog = false;
            var watchDog = Task.Factory.StartNew(async () =>
            {
                while (!restoreProcess.HasExited)
                {
                    if (DateTime.UtcNow - lastSignal > TimeSpan.FromSeconds(_options.PackageRestoreTimeout))
                    {
                        //_logger.LogError("killing restore comment ({0}) because it seems be stuck. retrying {1} more time(s)...", restoreProcess.Id, retry);
                        wasKilledByWatchDog = true;
                        restoreProcess.KillAll();
                    }
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            });

            restoreProcess.OutputDataReceived += (sender, e) =>
            {
                //_logger.LogInformation(e.Data);
                lastSignal = DateTime.UtcNow;
            };
            restoreProcess.ErrorDataReceived += (sender, e) =>
            {
                //_logger.LogError(e.Data);
                lastSignal = DateTime.UtcNow;
            };

            restoreProcess.BeginOutputReadLine();
            restoreProcess.BeginErrorReadLine();
            restoreProcess.WaitForExit();

            return wasKilledByWatchDog && retry > 0
                ? DoRun(project, retry - 1)
                : restoreProcess.ExitCode;
        }
    }
}
