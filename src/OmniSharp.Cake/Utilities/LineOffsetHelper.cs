using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmniSharp.Cake.Utilities
{
    internal static class LineOffsetHelper
    {
        public static async Task<int> GetOffset(string fileName, OmniSharpWorkspace workspace)
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

            var offset = sourceText.Lines.FirstOrDefault(line => line.ToString().Equals($"#line 1 \"{fullPath}\"")).LineNumber;

            return offset;
        }
    }
}
