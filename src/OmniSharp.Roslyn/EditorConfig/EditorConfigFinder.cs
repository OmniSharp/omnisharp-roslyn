// adapted from https://github.com/dotnet/format/blob/d8a66bbcc6b6b9e769eb168cb384b44328786f7b/src/Utilities/EditorConfigFinder.cs
// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.IO;
using System.Collections.Immutable;
using System.Linq;

namespace OmniSharp.Roslyn.EditorConfig
{
    public static class EditorConfigFinder
    {
        public static ImmutableArray<string> GetEditorConfigPaths(string path)
        {
            // If we are passed a filename then try to parse out the path
            if (!Directory.Exists(path) &&
                !TryGetDirectoryPath(path, out path))
            {
                return ImmutableArray<string>.Empty;
            }

            if (!Directory.Exists(path))
            {
                return ImmutableArray<string>.Empty;
            }

            var directory = new DirectoryInfo(path);

            var editorConfigPaths = directory.GetFiles(".editorconfig", SearchOption.AllDirectories)
                .Select(file => file.FullName)
                .ToList();

            try
            {
                while (directory.Parent is object)
                {
                    directory = directory.Parent;
                    editorConfigPaths.AddRange(
                        directory.GetFiles(".editorconfig", SearchOption.TopDirectoryOnly)
                            .Select(file => file.FullName));
                }
            }
            catch { }

            return editorConfigPaths.ToImmutableArray();
        }

        public static bool TryGetDirectoryPath(string path, out string directoryPath)
        {
            try
            {
                directoryPath = Path.GetDirectoryName(path);
                return true;
            }
            catch
            {
                directoryPath = default;
                return false;
            }
        }
    }
}
