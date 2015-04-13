using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.Logging;

namespace OmniSharp.AspNet5
{
    public class PackagesRestoreTool
    {
        private readonly object _lock = new object();
        private readonly ILogger _logger;
        private readonly AspNet5Context _context;
        private readonly AspNet5Paths _paths;
        private readonly IDictionary<string, Task<int>> _tasks;

        public PackagesRestoreTool(ILoggerFactory logger, AspNet5Context context, AspNet5Paths paths)
        {
            _logger = logger.Create<PackagesRestoreTool>();
            _context = context;
            _paths = paths;
            _tasks = new Dictionary<string, Task<int>>();
        }

        public void Run(Project project)
        {
            var workingDir = GetWorkingDir(project);
            var task = GetOrCreateRestoreTask(workingDir);

            task.ContinueWith(finishedTask =>
            {
                if (finishedTask.IsCanceled)
                {
                    return;
                }

                _context.Connection.Post(new Message()
                {
                    ContextId = project.ContextId,
                    MessageType = "RestoreComplete",
                    HostId = _context.HostId
                });
            });
        }

        private Task<int> GetOrCreateRestoreTask(string workingDir)
        {
            lock (_lock)
            {
                Task<int> task;
                if (_tasks.TryGetValue(workingDir, out task))
                {
                    return task;
                }

                Action<int> onRestoreDone = null;
                var tokenBefore = ComputeToken(workingDir);
                var tsc = new TaskCompletionSource<int>();
                task = tsc.Task;

                onRestoreDone = code =>
                {
                    var tokenAfter = ComputeToken(workingDir);
                    if (!Enumerable.SequenceEqual(tokenBefore, tokenAfter))
                    {
                        // once again
                        TryStartRestore(workingDir, onRestoreDone);
                        return;
                    }
                    else
                    {
                        lock (_lock)
                        {
                            _tasks.Remove(workingDir);
                        }
                        tsc.SetResult(code);
                    }
                };

                if (TryStartRestore(workingDir, onRestoreDone))
                {
                    _tasks[workingDir] = task;
                }
                else
                {
                    tsc.SetCanceled();
                }

                return task;
            }
        }

        private bool TryStartRestore(string workingDir, Action<int> onDone)
        {
            var psi = new ProcessStartInfo()
            {
                FileName = _paths.Dnu ?? _paths.Kpm,
                WorkingDirectory = workingDir,
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
                return false;
            }

            RedirectOutput(restoreProcess, _logger);

            restoreProcess.EnableRaisingEvents = true;
            restoreProcess.OnExit(() => onDone(restoreProcess.ExitCode));
            return true;
        }

        private static string GetWorkingDir(Project project)
        {
            return project.GlobalJsonPath != null
                ? Path.GetDirectoryName(project.GlobalJsonPath)
                : Path.GetDirectoryName(project.Path);
        }

        private static IEnumerable<DateTime> ComputeToken(string dir)
        {
            return Directory.GetFiles(dir)
                .Select(File.GetLastWriteTimeUtc)
                .OrderBy(element => element);
        }

        private static void RedirectOutput(Process process, ILogger logger)
        {
            Task.Factory.StartNew(async () =>
            {
                string line;
                while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                {
                    logger.WriteInformation(line);
                }
            });
            Task.Factory.StartNew(async () =>
            {
                string line;
                while ((line = await process.StandardError.ReadLineAsync()) != null)
                {
                    logger.WriteError(line);
                }
            });
        }
    }
}