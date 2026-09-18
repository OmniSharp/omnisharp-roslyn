using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace OmniSharp.Utilities
{
    public static class ProcessExtensions
    {
        public static void OnExit(this Process process, Action action)
        {
            process.Exited += (sender, e) =>
            {
                action();
            };
        }

        public static void KillChildrenAndThis(this Process process)
        {
            process.Kill();
        }

        private static IEnumerable<Process> GetChildProcesses(int processId)
        {
            foreach (var entry in GetAllProcessIds())
            {
                if (entry.parentId == processId)
                {
                    yield return Process.GetProcessById(entry.id);
                }
            }
        }

        private static IEnumerable<(int id, int parentId)> GetAllProcessIds()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ps",
                Arguments = string.Format("-o \"ppid, pid\" -ax"),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var entries = new List<(int processId, int parentProcessId)>();

            var ps = Process.Start(startInfo);
            ps.BeginOutputReadLine();
            ps.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                var parts = e.Data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (Int32.TryParse(parts[0], out var ppid) &&
                    Int32.TryParse(parts[1], out var pid))
                {
                    entries.Add((pid, ppid));
                }
            };

            ps.WaitForExit();

            return entries;
        }
    }
}
