using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
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
            var endLine = lineSpan.EndLinePosition.Line;

            string text;
            SourceText sourceText = null;

            // if we have a mapped linespan and we found a corresponding document, pick that one
            // otherwise use the SourceText of current location
            if (lineSpan.HasMappedPath && documents != null && documents.Any())
            {
                documents.First().TryGetText(out sourceText);
            }

            if (sourceText == null)
            {
                sourceText = location.SourceTree.GetText();
            }

            // bounds check in case the mapping is incorrect, since user can put whatever line they want
            if (sourceText.Lines.Count > line)
            {
                text = sourceText.Lines[line].ToString();
            }
            else
            {
                var fallBackLineSpan = location.GetLineSpan();

                // in case we fall out of bounds, we shouldn't crash, fallback to text from the unmapped span and the current file
                text = location.SourceTree.GetText().Lines[fallBackLineSpan.StartLinePosition.Line].ToString();
            }

            return new QuickFix
            {
                Text = text,
                FileName = path,
                Line = lineSpan.StartLinePosition.Line,
                Column = lineSpan.HasMappedPath ? 0 : lineSpan.StartLinePosition.Character, // when a #line directive maps into a separate file, assume columns (0,0)
                EndLine = lineSpan.EndLinePosition.Line,
                EndColumn = lineSpan.HasMappedPath ? 0 : lineSpan.EndLinePosition.Character,
                Projects = documents.Select(document => document.Project.Name).ToArray()
            };
        }
    }
}
