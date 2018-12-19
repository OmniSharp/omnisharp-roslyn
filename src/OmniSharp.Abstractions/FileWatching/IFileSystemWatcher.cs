using OmniSharp.Models.FilesChanged;
using System.IO;

namespace OmniSharp.FileWatching
{
    public delegate void FileSystemNotificationCallback(string filePath, FileChangeType changeType);

    public interface IFileSystemWatcher
    {
        /// <summary>
        /// Call to watch a file or directory path for changes.
        /// </summary>
        /// <param name="pathOrExtension">The file path, directory path or file extension to watch.</param>
        /// <param name="callback">The callback that will be invoked when a change occurs in the watched file or directory.</param>
        void Watch(string pathOrExtension, FileSystemNotificationCallback callback);

        /// <summary>
        /// Call to watch a a directory path (and child directory paths) for changes
        /// </summary>
        /// <param name="directory">The directory to watch.</param>
        /// <param name="callback">The callback that will be invoked when a change occurs in the directory.</param>
        void WatchRecursively(DirectoryInfo directory, FileSystemNotificationCallback callback);
    }
}
