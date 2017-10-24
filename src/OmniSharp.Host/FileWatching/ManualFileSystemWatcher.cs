using OmniSharp.Models.FilesChanged;
using System;
using System.Collections.Generic;
using System.IO;

namespace OmniSharp.FileWatching
{
    public class ManualFileSystemWatcher : IFileSystemWatcher
    {
        private readonly Dictionary<string, Action<string, FileChangeType?>> _callbacks = new Dictionary<string, Action<string, FileChangeType?>>();
        private readonly Dictionary<string, Action<string, FileChangeType?>> _directoryCallBacks = new Dictionary<string, Action<string, FileChangeType?>>();

        public void TriggerChange(string path, FileChangeType? verb)
        {
            if (_callbacks.TryGetValue(path, out var callback))
            {
                callback(path, verb);
            }

            var directoryPath = Path.GetDirectoryName(path);
            if (_directoryCallBacks.TryGetValue(directoryPath, out var fileCallback))
            {
                fileCallback(path, verb);
            }
        }

        public void Watch(string path, Action<string, FileChangeType?> callback)
        {
            _callbacks[path] = callback;
        }

        public void WatchDirectory(string path, Action<string, FileChangeType?> callback)
        {
            _directoryCallBacks[path] = callback;
        }
    }
}
