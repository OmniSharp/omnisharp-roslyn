using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace OmniSharp
{
    internal static class ProcessExtensions
    {
        private static object _syncLock = new object();
        private static Thread _backgroundWatcher;
        private static List<Tuple<Process, Action>> _processes = new List<Tuple<Process, Action>>();

        public static void OnExit(this Process process, Action action)
        {
            if (PlatformHelper.IsMono)
            {
                // In mono 3.10, the Exited event fires immediately, we're going to poll instead
                lock (_syncLock)
                {
                    _processes.Add(Tuple.Create(process, action));

                    if (_backgroundWatcher == null)
                    {
                        _backgroundWatcher = new Thread(WatchProcesses) { IsBackground = true };
                        _backgroundWatcher.Start();
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

        public static void KillAll(this Process process)
        {
            if (PlatformHelper.IsMono)
            {
                foreach (var pe in GetChildren(process.Id))
                {
                    Process.GetProcessById(pe.ProcessId).Kill();
                }
            }

            process.Kill();
        }

        private static IEnumerable<ProcessEntry> GetChildren(int processId)
        {
            return GetProcesses().Where(pe => pe.ParentProcessId == processId);
        }

        private static IEnumerable<ProcessEntry> GetProcesses()
        {
            var si = new ProcessStartInfo
            {
                FileName = "ps",
                Arguments = string.Format("-o \"ppid, pid\" -ax"),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var processEntries = new List<ProcessEntry>();

            var ps = Process.Start(si);
            ps.BeginOutputReadLine();
            ps.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                var parts = e.Data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int ppid;
                int pid;
                if (Int32.TryParse(parts[0], out ppid) &&
                   Int32.TryParse(parts[1], out pid))
                {
                    processEntries.Add(new ProcessEntry
                    {
                        ProcessId = pid,
                        ParentProcessId = ppid
                    });
                }

            };

            ps.WaitForExit();

            return processEntries;
        }

        private static void WatchProcesses()
        {
            while (true)
            {
                lock (_processes)
                {
                    for (int i = _processes.Count - 1; i >= 0; --i)
                    {
                        var pair = _processes[i];
                        if (pair.Item1.HasExited)
                        {
                            _processes.RemoveAt(i);
                            pair.Item2();
                        }
                    }
                }

                // REVIEW: Configurable?
                Thread.Sleep(2000);
            }
        }

        private class ProcessEntry
        {
            public int ProcessId { get; set; }
            public int ParentProcessId { get; set; }
        }
    }
}