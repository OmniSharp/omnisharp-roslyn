using System;
using System.Collections.Generic;
using System.IO;

namespace OmniSharp
{
    public class FileWatcher
    {
        private readonly HashSet<string> _files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly FileSystemWatcher _watcher;

        internal FileWatcher()
        {

        }

        public FileWatcher(string path)
        {
            _watcher = new FileSystemWatcher(path);
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;

            _watcher.Changed += OnWatcherChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.Deleted += OnWatcherChanged;
            _watcher.Created += OnWatcherChanged;
        }

        public event Action<string, WatcherChangeTypes> OnChanged;
        
        public bool WatchFile(string path)
        {
            return _files.Add(path);
        }

        public void Dispose()
        {
            _watcher.Dispose();
        }

        public bool ReportChange(string newPath, WatcherChangeTypes changeType)
        {
            return ReportChange(oldPath: null, newPath: newPath, changeType: changeType);
        }

        public bool ReportChange(string oldPath, string newPath, WatcherChangeTypes changeType)
        {
            if (HasChanged(oldPath, newPath, changeType))
            {
                if (oldPath != null)
                {
                    Console.WriteLine("{0} -> {1}", oldPath, newPath);
                }
                else
                {
                    Console.WriteLine("{0} -> {1}", changeType, newPath);
                }

                if (OnChanged != null)
                {
                    OnChanged(oldPath ?? newPath, changeType);
                }

                return true;
            }

            return false;
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            ReportChange(e.OldFullPath, e.FullPath, e.ChangeType);
        }

        private void OnWatcherChanged(object sender, FileSystemEventArgs e)
        {
            ReportChange(e.FullPath, e.ChangeType);
        }

        private bool HasChanged(string oldPath, string newPath, WatcherChangeTypes changeType)
        {
            // File changes
            if (_files.Contains(newPath) ||
                (oldPath != null && _files.Contains(oldPath)))
            {
                return true;
            }

            return false;
        }
    }
}