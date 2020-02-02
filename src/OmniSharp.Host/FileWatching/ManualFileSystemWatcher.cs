using System;
using System.Collections.Generic;
using System.IO;

namespace OmniSharp.FileWatching
{
    internal class ManualFileSystemWatcher : IFileSystemWatcher, IFileSystemNotifier
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, Callbacks> _callbacksMap;

        public ManualFileSystemWatcher()
        {
            _callbacksMap = new Dictionary<string, Callbacks>(StringComparer.OrdinalIgnoreCase);
        }

        public void Notify(string filePath, FileChangeType changeType = FileChangeType.Unspecified)
        {
            lock (_gate)
            {
                if (_callbacksMap.TryGetValue(filePath, out var fileCallbacks))
                {
                    fileCallbacks.Invoke(filePath, changeType);
                }

                var directoryPath = Path.GetDirectoryName(filePath);
                if (_callbacksMap.TryGetValue(directoryPath, out var directoryCallbacks))
                {
                    directoryCallbacks.Invoke(filePath, changeType);
                }

                var extension = Path.GetExtension(filePath);
                if (!string.IsNullOrEmpty(extension) &&
                    _callbacksMap.TryGetValue(extension, out var extensionCallbacks))
                {
                    extensionCallbacks.Invoke(filePath, changeType);
                }
            }
        }

        public void Watch(string pathOrExtension, FileSystemNotificationCallback callback)
        {
            if (pathOrExtension == null)
            {
                throw new ArgumentNullException(nameof(pathOrExtension));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            lock (_gate)
            {
                if (!_callbacksMap.TryGetValue(pathOrExtension, out var callbacks))
                {
                    callbacks = new Callbacks();
                    _callbacksMap.Add(pathOrExtension, callbacks);
                }

                callbacks.Add(callback);
            }
        }

        public void Unwatch(string pathOrExtension)
        {
            if (pathOrExtension == null)
            {
                throw new ArgumentNullException(nameof(pathOrExtension));
            }

            lock (_gate)
            {
                if (_callbacksMap.ContainsKey(pathOrExtension))
                {
                    _callbacksMap.Remove(pathOrExtension);
                }
            }
        }

        private class Callbacks
        {
            private readonly List<FileSystemNotificationCallback> _callbacks = new List<FileSystemNotificationCallback>();
            private readonly HashSet<FileSystemNotificationCallback> _callbackSet = new HashSet<FileSystemNotificationCallback>();

            public void Add(FileSystemNotificationCallback callback)
            {
                if (_callbackSet.Add(callback))
                {
                    _callbacks.Add(callback);
                }
            }

            public void Invoke(string filePath, FileChangeType changeType)
            {
                foreach (var callback in _callbacks)
                {
                    callback(filePath, changeType);
                }
            }
        }
    }
}
