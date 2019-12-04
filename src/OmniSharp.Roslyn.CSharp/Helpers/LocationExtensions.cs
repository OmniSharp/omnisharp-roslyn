using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;

namespace OmniSharp.Helpers
{
    public static class LocationExtensions
    {
        public static QuickFix GetQuickFix(this Location location, OmniSharpWorkspace workspace)
        {
            if (!location.IsInSource)
                throw new Exception("Location is not in the source tree");

            var lineSpan = Path.GetExtension(location.SourceTree.FilePath).Equals(".cake", StringComparison.OrdinalIgnoreCase)
                ? location.GetLineSpan()
                : location.GetMappedLineSpan();
            var path = lineSpan.Path;
            var documents = workspace.GetDocuments(path);

            var line = lineSpan.StartLinePosition.Line;
            var text = location.SourceTree.GetText().Lines[line].ToString();

            return new QuickFix
            {
                Text = text.Trim(),
                FileName = path,
                Line = line,
                Column = lineSpan.HasMappedPath ? 0 : lineSpan.StartLinePosition.Character, // when a #line directive maps into a separate file, assume columns (0,0)
                EndLine = lineSpan.EndLinePosition.Line,
                EndColumn = lineSpan.HasMappedPath ? 0 : lineSpan.EndLinePosition.Character,
                Projects = documents.Select(document => document.Project.Name).ToArray()
            };
        }
    }
}
