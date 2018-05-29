using System;
using System.IO;

namespace TestUtility
{
    public class DisposableFile : IDisposable
    {
        public DisposableFile(string fileName, string contents = null)
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetTempFileName()));

            DirectoryPath = tempFolder;
            FilePath = Path.Combine(tempFolder, Path.GetFullPath(fileName));

            Directory.CreateDirectory(tempFolder);
            File.WriteAllText(FilePath, contents ?? string.Empty);
        }

        public string FilePath { get; }

        public string DirectoryPath { get; }


        public void Dispose()
        {
            RemoveDirectory(DirectoryPath);
        }

        private static void RemoveDirectory(string path)
        {
            if (!Directory.Exists(path)) return;

            NormalizeAttributes(path);

            foreach (string directory in Directory.GetDirectories(path))
            {
                RemoveDirectory(directory);
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (Exception)
            {
                // best effort
                // these are TEMP directory files anyway
            }

            void NormalizeAttributes(string directoryPath)
            {
                string[] filePaths = Directory.GetFiles(directoryPath);
                string[] subdirectoryPaths = Directory.GetDirectories(directoryPath);

                foreach (string filePath in filePaths)
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                }

                foreach (string subdirectoryPath in subdirectoryPaths)
                {
                    NormalizeAttributes(subdirectoryPath);
                }

                File.SetAttributes(directoryPath, FileAttributes.Normal);
            }
        }
    }
}
