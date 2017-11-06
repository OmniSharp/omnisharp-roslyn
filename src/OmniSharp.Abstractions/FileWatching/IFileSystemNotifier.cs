using OmniSharp.Models.FilesChanged;

namespace OmniSharp.FileWatching
{
    public interface IFileSystemNotifier
    {
        /// <summary>
        /// Notifiers any relevant file system watchers when a file is created, changed, or deleted.
        /// </summary>
        /// <param name="filePath">The path to the file that was changed.</param>
        /// <param name="changeType">The type of change. Hosts are not required to pass a change type.</param>
        void Notify(string filePath, FileChangeType changeType = FileChangeType.Unspecified);
    }
}
