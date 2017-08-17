using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmniSharp.Cake.Utilities
{
    internal static class LineOffsetHelper
    {
        public static async Task<int> GetOffset(string fileName, int target, OmniSharpWorkspace workspace)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return 0;
            }

            var document = workspace.GetDocument(fileName);
            if (document == null)
            {
                return 0;
            }

            var fullPath = Path.GetFullPath(fileName).Replace('\\','/');
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
                if (target >= lineNumber)
                {
                    return sourceText.Lines[i].LineNumber - lineNumber + 2;
                }
            }

            return 0;
        }
    }
}
