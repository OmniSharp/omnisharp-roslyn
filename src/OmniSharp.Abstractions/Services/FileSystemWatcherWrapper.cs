using System;
using System.Collections.Generic;
using System.IO;

namespace OmniSharp.Services
{
    public class FileSystemWatcherWrapper : IFileSystemWatcher
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Dictionary<string, Action<string>> _callbacks = new Dictionary<string, Action<string>>();
        private readonly List<Action<string>> _globalCallbacks = new List<Action<string>>();

        public FileSystemWatcherWrapper(IOmnisharpEnvironment env)
        {
            // Environment.SetEnvironmentVariable ("MONO_MANAGED_WATCHER", "1");
            _watcher = new FileSystemWatcher(env.Path);
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += (sender, e) =>
            {
                TriggerChange(e.OldFullPath);
                TriggerChange(e.FullPath);
            };
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            TriggerChange(e.FullPath);
        }

        public void TriggerChange(string path)
        {
            Action<string> callback;
            if (_callbacks.TryGetValue(path, out callback))
            {
                callback(path);
            }
            foreach (var globalCallback in _globalCallbacks)
            {
                globalCallback(path);
            }
        }

        public void Watch(string path, Action<string> callback)
        {
            _callbacks[path] = callback;
        }
        
        public void WatchGlobal(Action<string> callback)
        {
            _globalCallbacks.Add(callback);
        }
    }
}