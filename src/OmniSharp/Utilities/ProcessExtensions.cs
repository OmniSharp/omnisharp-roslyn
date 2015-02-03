using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    }
}