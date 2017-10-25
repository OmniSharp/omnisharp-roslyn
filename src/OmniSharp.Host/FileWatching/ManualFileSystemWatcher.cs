using System;
using System.Collections.Generic;
using System.IO;
using OmniSharp.Models.FilesChanged;

namespace OmniSharp.FileWatching
{
    public class ManualFileSystemWatcher : IFileSystemWatcher
    {
        private readonly Dictionary<string, Action<string, FileChangeType>> _callbacks = new Dictionary<string, Action<string, FileChangeType>>();
        private readonly Dictionary<string, Action<string, FileChangeType>> _directoryCallBacks = new Dictionary<string, Action<string, FileChangeType>>();

        public void TriggerChange(string path, FileChangeType changeType)
        {
            if (_callbacks.TryGetValue(path, out var callback))
            {
                callback(path, changeType);
            }

            var directoryPath = Path.GetDirectoryName(path);
            if (_directoryCallBacks.TryGetValue(directoryPath, out var fileCallback))
            {
                fileCallback(path, changeType);
            }
        }

        public void Watch(string path, Action<string, FileChangeType> callback)
        {
            _callbacks[path] = callback;
        }

        public void WatchDirectory(string path, Action<string, FileChangeType> callback)
        {
            if (_directoryCallBacks.TryGetValue(path, out var existingCallback))
            {
                _directoryCallBacks[path] = (Action<string, FileChangeType>)Delegate.Combine(callback, existingCallback);
            }
            else
            {
                _directoryCallBacks[path] = callback;
            }
        }
    }
}
