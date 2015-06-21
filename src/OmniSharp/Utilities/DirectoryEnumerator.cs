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

        public IEnumerable<string> SafeEnumerateFiles(string path, string searchPattern)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                yield break;
            }
            
            string[] files = null;
            string[] directories = null;
            
            try
            {
                // Get the files and directories now so we can get any exceptions up front
                files = Directory.GetFiles(path, searchPattern);
                directories = Directory.GetDirectories(path);
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning(string.Format("Unauthorized access to {0}, skipping", path));
                yield break;
            }
            catch (PathTooLongException)
            {
                _logger.LogWarning(string.Format("Path {0} is too long, skipping", path));
                yield break;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var file in directories.SelectMany(x => SafeEnumerateFiles(x, searchPattern)))
            {
                yield return file;
            }
        }
    }
}
