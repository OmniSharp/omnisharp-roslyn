using System;

namespace OmniSharp.Services
{
    // TODO: Flesh out this API more
    public interface IFileSystemWatcher
    {
        void Watch(string path, Action<string> callback);

        void TriggerChange(string path);
    }
}