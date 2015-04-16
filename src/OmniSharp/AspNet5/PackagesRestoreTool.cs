using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.Logging;
using OmniSharp.Models;
using OmniSharp.Services;

namespace OmniSharp.AspNet5
{
    public class PackagesRestoreTool
    {
        private readonly ILogger _logger;
        private readonly IEventEmitter _emitter;
        private readonly AspNet5Context _context;
        private readonly AspNet5Paths _paths;
        private readonly object _lock;
        private readonly IDictionary<string, object> _projectLocks;
        private readonly SemaphoreSlim _semaphore;

        public PackagesRestoreTool(ILoggerFactory logger, IEventEmitter emitter, AspNet5Context context, AspNet5Paths paths)
        {
            _logger = logger.Create<PackagesRestoreTool>();
            _emitter = emitter;
            _context = context;
            _paths = paths;
            _lock = new object();
            _projectLocks = new Dictionary<string, object>();
            _semaphore = new SemaphoreSlim(Environment.ProcessorCount / 2);
        }

        public void Run(Project project)
        {
            Task.Factory.StartNew(() =>
            {
                this._emitter.Emit(EventTypes.PackageRestoreStarted, new PackageRestoreMessage()
                {
                    ProjectFileName = project.Path
                });

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
                    _semaphore.Wait();
                    try
                    {
                        var psi = new ProcessStartInfo()
                        {
                            FileName = _paths.Dnu ?? _paths.Kpm,
                            WorkingDirectory = Path.GetDirectoryName(project.Path),
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            Arguments = "restore"
                        };

                        _logger.WriteInformation("restore packages {0} {1} for {2}", psi.FileName, psi.Arguments, psi.WorkingDirectory);

                        var restoreProcess = Process.Start(psi);
                        if (restoreProcess.HasExited)
                        {
                            _logger.WriteError("restore command ({0}) failed with error code {1}", psi.FileName, restoreProcess.ExitCode);
                        }
                        else
                        {
                            restoreProcess.OutputDataReceived += (sender, e) => _logger.WriteInformation(e.Data);
                            restoreProcess.ErrorDataReceived += (sender, e) => _logger.WriteError(e.Data);
                            restoreProcess.BeginOutputReadLine();
                            restoreProcess.BeginErrorReadLine();
                            restoreProcess.WaitForExit();

                            _context.Connection.Post(new Message()
                            {
                                ContextId = project.ContextId,
                                MessageType = "RestoreComplete",
                                HostId = _context.HostId
                            });
                        }

                        this._emitter.Emit(EventTypes.PackageRestoreFinished, new PackageRestoreMessage()
                        {
                            ProjectFileName = project.Path,
                            Success = restoreProcess.ExitCode == 0
                        });
                    }
                    finally
                    {
                        _semaphore.Release();
                        _projectLocks.Remove(project.Path);
                    }
                }
            });
        }
    }
}
