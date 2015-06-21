using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.Logging;

namespace OmniSharp.Utilities
{
    public class DirectoryEnumerator
    {
        private ILogger _logger;

        public DirectoryEnumerator(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DirectoryEnumerator>();
        }

        public IEnumerable<string> SafeEnumerateFiles(string target, string pattern = "*.*")
        {
            var allFiles = Enumerable.Empty<string>();

            var directoryStack = new Stack<string>();
            directoryStack.Push(target);

            while (directoryStack.Any())
            {
                var current = directoryStack.Pop();

                try
                {
                    var files = Directory.GetFiles(current, pattern);
                    allFiles = allFiles.Concat(files);

                    foreach (var subdirectory in Directory.EnumerateDirectories(current))
                    {
                        directoryStack.Push(subdirectory);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogWarning(string.Format("Unauthorized access to {0}, skipping", current));
                }
                catch (PathTooLongException)
                {
                    _logger.LogWarning(string.Format("Path {0} is too long, skipping", current));
                }
            }

            return allFiles;
        }
    }
}
