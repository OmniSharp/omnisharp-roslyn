using System.Collections.Generic;

namespace OmniSharp.FileWatching
{
    internal partial class ManualFileSystemWatcher
    {
        private class Callbacks
        {
            private List<FileSystemNotificationCallback> _callbacks = new List<FileSystemNotificationCallback>();
            private HashSet<FileSystemNotificationCallback> _callbackSet = new HashSet<FileSystemNotificationCallback>();

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
