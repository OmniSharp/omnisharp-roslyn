using System;
using System.Collections.Generic;
using System.IO;
using OmniSharp.Models.FilesChanged;

namespace OmniSharp.FileWatching
{
    internal class ManualFileSystemWatcher : IFileSystemWatcher, IFileSystemNotifier
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, FileSystemNotificationCallback> _callbacks;

        public ManualFileSystemWatcher()
        {
            _callbacks = new Dictionary<string, FileSystemNotificationCallback>(StringComparer.OrdinalIgnoreCase);
        }

        public void Notify(string filePath, FileChangeType changeType = FileChangeType.Unspecified)
        {
            lock (_gate)
            {
                if (_callbacks.TryGetValue(filePath, out var fileCallback))
                {
                    fileCallback(filePath, changeType);
                }

                var directoryPath = Path.GetDirectoryName(filePath);
                if (_callbacks.TryGetValue(directoryPath, out var directoryCallback))
                {
                    directoryCallback(filePath, changeType);
                }

                var extension = Path.GetExtension(filePath);
                if (!string.IsNullOrEmpty(extension) &&
                    _callbacks.TryGetValue(extension, out var extensionCallback))
                {
                    extensionCallback(filePath, changeType);
                }
            }
        }

        public void Watch(string pathOrExtension, FileSystemNotificationCallback callback)
        {
            lock (_gate)
            {
                if (_callbacks.TryGetValue(pathOrExtension, out var existingCallback))
                {
                    callback = callback + existingCallback;
                }

                _callbacks[pathOrExtension] = callback;
            }
        }
    }
}
