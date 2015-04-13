using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.Logging;

namespace OmniSharp.AspNet5
{
    public class PackagesRestoreTool
    {
        private readonly ILogger _logger;
        private readonly AspNet5Context _context;
        private readonly AspNet5Paths _paths;
        private readonly object _lock;
        private readonly IDictionary<string, AutoResetEvent> _gates;
        private readonly SemaphoreSlim _semaphore;

        public PackagesRestoreTool(ILoggerFactory logger, AspNet5Context context, AspNet5Paths paths)
        {
            _logger = logger.Create<PackagesRestoreTool>();
            _context = context;
            _paths = paths;
            _lock = new object();
            _gates = new Dictionary<string, AutoResetEvent>();
            _semaphore = new SemaphoreSlim(Environment.ProcessorCount / 2);
        }

        public void Run(Project project)
        {
            AutoResetEvent gate;
            lock (_lock)
            {
                if (!_gates.TryGetValue(project.Path, out gate))
                {
                    gate = new AutoResetEvent(true);
                    _gates.Add(project.Path, gate);
                }
            }

            gate.WaitOne();
            _semaphore.Wait();

            TryStartRestore(Path.GetDirectoryName(project.Path), (didStart) =>
            {
                _semaphore.Release();
                gate.Set();
                lock (_lock)
                {
                    _gates.Remove(project.Path);
                }

                if (didStart)
                {
                    _context.Connection.Post(new Message()
                    {
                        ContextId = project.ContextId,
                        MessageType = "RestoreComplete",
                        HostId = _context.HostId
                    });
                }
            });
        }

        private void TryStartRestore(string workingDir, Action<bool> onDone)
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
                onDone(false);
            }
            else
            {
                RedirectOutput(restoreProcess, _logger);
                restoreProcess.EnableRaisingEvents = true;
                restoreProcess.OnExit(() => onDone(true));
            }
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
