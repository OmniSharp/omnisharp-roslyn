using System;
using System.Diagnostics;
using System.IO;
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

        public PackagesRestoreTool(ILoggerFactory logger, AspNet5Context context, AspNet5Paths paths)
        {
            _logger = logger.Create<PackagesRestoreTool>();
            _context = context;
            _paths = paths;
        }

        public Task<int> Run(Project project)
        {
            var tsc = new TaskCompletionSource<int>();
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

            _logger.WriteInformation("restore packages with {0} {1}", psi.FileName, psi.Arguments);

            var restoreProcess = Process.Start(psi);
            if (restoreProcess.HasExited)
            {
                tsc.SetException(new Exception("Failed to start process: " + psi.FileName));
            }
            else
            {
                ReadAndWriteOutput(restoreProcess, _logger);
                restoreProcess.EnableRaisingEvents = true;
                restoreProcess.OnExit(() =>
                {
                    _context.Connection.Post(new Message()
                    {
                        ContextId = project.ContextId,
                        MessageType = "RestoreComplete",
                        HostId = _context.HostId
                    });
                    tsc.SetResult(restoreProcess.ExitCode);
                });
            }

            return tsc.Task;
        }

        private static void ReadAndWriteOutput(Process process, ILogger logger)
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