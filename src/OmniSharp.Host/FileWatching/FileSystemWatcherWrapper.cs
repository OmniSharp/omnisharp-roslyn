using OmniSharp.Models.FilesChanged;
using System;
using System.Collections.Generic;
using System.IO;

namespace OmniSharp.FileWatching
{
    public class FileSystemWatcherWrapper : IFileSystemWatcher
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Dictionary<string, Action<string, FileChangeType?>> _callbacks = new Dictionary<string, Action<string, FileChangeType?>>();

        public FileSystemWatcherWrapper(IOmniSharpEnvironment env)
        {
            // Environment.SetEnvironmentVariable ("MONO_MANAGED_WATCHER", "1");
            _watcher = new FileSystemWatcher(env.TargetDirectory)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
            };

            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;

            _watcher.Renamed += (sender, e) =>
            {
                TriggerChange(e.OldFullPath, FileChangeType.Delete);
                TriggerChange(e.FullPath, FileChangeType.Create);
            };
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            TriggerChange(e.FullPath, Convert(e.ChangeType));
        }

        private FileChangeType? Convert(WatcherChangeTypes change)
        {
            switch (change)
            {
                case WatcherChangeTypes.Created:
                    return FileChangeType.Create;
                case WatcherChangeTypes.Deleted:
                    return FileChangeType.Delete;
                default:
                    throw new ArgumentException($"unexpected value {change}");
            }
        }

        public void TriggerChange(string path, FileChangeType? verb)
        {
            if (_callbacks.TryGetValue(path, out var callback))
            {
                callback(path, verb);
            }
        }

        public void Watch(string path, Action<string, FileChangeType?> callback)
        {
            _callbacks[path] = callback;
        }

        public void WatchDirectory(string path, Action<string, FileChangeType?> callback) => throw new NotImplementedException();
    }
}
