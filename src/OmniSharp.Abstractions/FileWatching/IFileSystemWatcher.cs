using OmniSharp.Models.FilesChanged;

namespace OmniSharp.FileWatching
{
    public delegate void FileSystemNotificationCallback(string filePath, FileChangeType changeType);

    public interface IFileSystemWatcher
    {
        /// <summary>
        /// Call to watch a file or directory path for changes.
        /// </summary>
        /// <param name="fileOrDirectoryPath">The file or directory path to watch.</param>
        /// <param name="callback">The callback that will be invoked when a change occurs in the watched file or directory.</param>
        void Watch(string fileOrDirectoryPath, FileSystemNotificationCallback callback);
    }
}
