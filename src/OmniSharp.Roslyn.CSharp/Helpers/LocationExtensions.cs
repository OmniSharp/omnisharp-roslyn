using System;
using System.Collections.Generic;
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

            var documents = workspace.GetDocuments(lineSpan.Path);
            (var sourceText, var isMappedDocument) = GetSourceText(location, documents, lineSpan.HasMappedPath);
            var text = GetText(location, sourceText, lineSpan.StartLinePosition.Line, isMappedDocument);

            return new QuickFix
            {
                Text = text,
                FileName = lineSpan.Path,
                Line = lineSpan.StartLinePosition.Line,
                Column = lineSpan.HasMappedPath ? 0 : lineSpan.StartLinePosition.Character, // when a #line directive maps into a separate file, assume columns (0,0)
                EndLine = lineSpan.EndLinePosition.Line,
                EndColumn = lineSpan.HasMappedPath ? 0 : lineSpan.EndLinePosition.Character,
                Projects = documents.Select(document => document.Project.Name).ToArray()
            };

            static (SourceText sourceText, bool isMappedDocument) GetSourceText(Location location, IEnumerable<Document> documents, bool hasMappedPath)
            {
                // if we have a mapped linespan and we found a corresponding document, pick that one
                // otherwise use the SourceText of current location
                if (hasMappedPath && documents != null && documents.Any())
                {
                    if (documents.First().TryGetText(out SourceText sourceText))
                    {
                        return (sourceText, true);
                    }
                }

                return (location.SourceTree.GetText(), false);
            }

            static string GetText(Location location, SourceText sourceText, int startLine, bool isMappedDocument)
            {
                // bounds check in case the mapping is incorrect, since user can put whatever line they want
                if (isMappedDocument && sourceText.Lines.Count > startLine)
                {
                    return sourceText.Lines[startLine].ToString();
                }

                // in case we fall out of bounds, we shouldn't crash, fallback to text from the unmapped span and the current file
                var fallBackLineSpan = location.GetLineSpan();
                return location.SourceTree.GetText().Lines[fallBackLineSpan.StartLinePosition.Line].ToString();
            }
        }
    }
}
