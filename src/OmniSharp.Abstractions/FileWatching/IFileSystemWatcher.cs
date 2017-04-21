using System;

namespace OmniSharp.FileWatching
{
    // TODO: Flesh out this API more
    public interface IFileSystemWatcher
    {
        void Watch(string path, Action<string> callback);

        void TriggerChange(string path);
    }
}