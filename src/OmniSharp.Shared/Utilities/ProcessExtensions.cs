using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace OmniSharp.Utilities
{
    public static class ProcessExtensions
    {
        private static Thread s_backgroundWatcher;
        private static List<(Process process, Action action)> s_watchedProcesses;

        public static void OnExit(this Process process, Action action)
        {
            var gate = new object();

            void CleanUpProcesses()
            {
                lock (gate)
                {
                    for (int i = s_watchedProcesses.Count - 1; i >= 0; --i)
                    {
                        var (p, a) = s_watchedProcesses[i];
                        if (p.HasExited)
                        {
                            s_watchedProcesses.RemoveAt(i);
                            a();
                        }
                    }
                }
            }

            void Watcher()
            {
                while (true)
                {
                    CleanUpProcesses();

                    // REVIEW: Configurable?
                    Thread.Sleep(2000);
                }
            }

            if (PlatformHelper.IsMono)
            {
                // In mono 3.10, the Exited event fires immediately, we're going to poll instead
                lock (gate)
                {
                    if (s_watchedProcesses == null)
                    {
                        s_watchedProcesses = new List<(Process process, Action action)>();
                    }

                    s_watchedProcesses.Add((process, action));

                    if (s_backgroundWatcher == null)
                    {
                        s_backgroundWatcher = new Thread(Watcher) { IsBackground = true };
                        s_backgroundWatcher.Start();
                    }
                }
            }
            else
            {
                process.Exited += (sender, e) =>
                {
                    action();
                };
            }
        }

        public static void KillChildrenAndThis(this Process process)
        {
            if (PlatformHelper.IsMono)
            {
                foreach (var childProcess in GetChildProcesses(process.Id))
                {
                    childProcess.Kill();
                }
            }

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
