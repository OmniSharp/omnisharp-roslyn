using System;
using System.IO;
using System.Threading.Tasks;
using OmniSharp.Utilities;

namespace OmniSharp.Cake.Utilities
{
    internal static class LineIndexHelper
    {
        public static async Task<int> TranslateToGenerated(string fileName, int index, OmniSharpWorkspace workspace)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return index;
            }

            var document = workspace.GetDocument(fileName);
            if (document == null)
            {
                return index;
            }

            var fullPath = Path.GetFullPath(fileName);
            if (PlatformHelper.IsWindows)
            {
                // Cake alwyas normalizes dir separators to /
                fullPath = fullPath.Replace('\\', '/');
            }
            var sourceText = await document.GetTextAsync();
            for (var i = sourceText.Lines.Count - 1; i >= 0; i--)
            {
                var text = sourceText.Lines[i].ToString();

                if (!text.StartsWith("#line ") || !text.EndsWith($" \"{fullPath}\""))
                {
                    continue;
                }

                var tokens = text.Split(' ');
                if (tokens.Length <= 2 || !int.TryParse(tokens[1], out int lineNumber))
                {
                    continue;
                }
                lineNumber--;
                if (index >= lineNumber)
                {
                    return sourceText.Lines[i].LineNumber - lineNumber + index + 1;
                }
            }

            return index;
        }

        public static async Task<(int, string)> TranslateFromGenerated(string fileName, int index, OmniSharpWorkspace workspace, bool sameFile)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return (-1, fileName);
            }

            if (PlatformHelper.IsWindows)
            {
                fileName = fileName.Replace('/', '\\');
            }
            var document = workspace.GetDocument(fileName);
            if (document == null)
            {
                return (-1, fileName);
            }

            var sourceText = await document.GetTextAsync();
            if (index > sourceText.Lines.Count)
            {
                return (-1, fileName);
            }

            for (var i = index; i >= 0; i--)
            {
                var text = sourceText.Lines[i].ToString();

                if (!text.StartsWith("#line "))
                {
                    continue;
                }

                var tokens = text.Split(new[] {' '}, 3, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 3 || !int.TryParse(tokens[1], out int lineNumber))
                {
                    continue;
                }

                var newFileName = tokens[2].Trim('"');
                if (PlatformHelper.IsWindows)
                {
                    newFileName = newFileName.Replace('/', '\\');
                }
                if (sameFile && !newFileName.Equals(fileName))
                {
                    return (-1, fileName);
                }

                var newIndex = lineNumber - 2 + (index - i);

                return (newIndex, newFileName);
            }

            return (-1, fileName);
        }
    }
}
