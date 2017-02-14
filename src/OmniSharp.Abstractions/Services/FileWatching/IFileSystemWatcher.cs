using System;

namespace OmniSharp.Services.FileWatching
{
    // TODO: Flesh out this API more
    public interface IFileSystemWatcher
    {
        void Watch(string path, Action<string> callback);

        void TriggerChange(string path);
    }
}