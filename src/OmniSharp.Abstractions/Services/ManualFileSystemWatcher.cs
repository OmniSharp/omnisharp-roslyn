using System;
using System.Collections.Generic;

namespace OmniSharp.Services
{
    public class ManualFileSystemWatcher : IFileSystemWatcher
    {
        private readonly Dictionary<string, Action<string>> _callbacks = new Dictionary<string, Action<string>>();

        public void TriggerChange(string path)
        {
            Action<string> callback;
            if (_callbacks.TryGetValue(path, out callback))
            {
                callback(path);
            }
        }

        public void Watch(string path, Action<string> callback)
        {
            _callbacks[path] = callback;
        }
    }
}