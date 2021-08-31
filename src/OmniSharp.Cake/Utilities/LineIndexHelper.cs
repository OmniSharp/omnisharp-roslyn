using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Extensions;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.Extensions;
using OmniSharp.Utilities;
using Range = OmniSharp.Models.V2.Range;

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

            var sourceText = await document.GetTextAsync();
            var root = await document.GetSyntaxRootAsync();

            for (var i = sourceText.Lines.Count - 1; i >= 0; i--)
            {
                var line = sourceText.Lines[i];

                if (!line.StartsWith("#line "))
                {
                    continue;
                }

                if (!(root.FindNode(line.Span, true) is LineDirectiveTriviaSyntax lineDirective) ||
                    lineDirective.Line.IsMissing ||
                    !PathsAreEqual((string)lineDirective.File.Value, fileName))
                {
                    continue;
                }

                var lineDirectiveValue = (int)lineDirective.Line.Value - 1;

                if (index < lineDirectiveValue)
                {
                    continue;
                }

                return line.LineNumber - lineDirectiveValue + index + 1;
            }

            return index;
        }

        public static async Task<(int, string)> TranslateFromGenerated(string fileName, int index, OmniSharpWorkspace workspace, bool sameFile)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return (-1, fileName);
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

            var syntaxTree = await document.GetSyntaxTreeAsync(CancellationToken.None);
            if (syntaxTree == null)
            {
                return (-1, fileName);
            }

            var point = new Point { Column = 0, Line = index };
            var textSpan = sourceText.GetSpanFromRange(new Range
            {
                Start = point,
                End = point
            });

            var lineMapping = syntaxTree.GetMappedLineSpan(textSpan);

            if (sameFile && !PathsAreEqual(lineMapping.Path, fileName))
            {
                return (-1, fileName);
            }

            return (lineMapping.StartLinePosition.Line, PlatformHelper.IsWindows ? lineMapping.Path.Replace('/', '\\') : lineMapping.Path);
        }

        private static bool PathsAreEqual(string x, string y)
        {
            if (x == null && y == null)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            var comparer = PlatformHelper.IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            return Path.GetFullPath(x).Equals(Path.GetFullPath(y), comparer);
        }
    }
}
