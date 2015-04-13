using System;
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
        private readonly ILogger _logger;
        private readonly AspNet5Context _context;

        public PackagesRestoreTool(ILoggerFactory logger, AspNet5Context context)
        {
            _logger = logger.Create<PackagesRestoreTool>();
            _context = context;
        }

        public Task<int> Run(string runtimePath, Project project)
        {
            var tsc = new TaskCompletionSource<int>();
            var psi = new ProcessStartInfo()
            {
                FileName = GetRuntimeExecutable(runtimePath),
                WorkingDirectory = Path.GetDirectoryName(project.Path),
                CreateNoWindow = true,
                UseShellExecute = false,
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

        private static string GetRuntimeExecutable(string runtimePath)
        {
            var newPath = Path.Combine(runtimePath, "bin", "dnu");
            var newPathCmd = Path.Combine(runtimePath, "bin", "dnu.cmd");
            var oldPath = Path.Combine(runtimePath, "bin", "kpm");
            var oldPathCmd = Path.Combine(runtimePath, "bin", "kpm.cmd");

            return new[] { newPath, newPathCmd, oldPath, oldPathCmd }.First(File.Exists);
        }
    }
}