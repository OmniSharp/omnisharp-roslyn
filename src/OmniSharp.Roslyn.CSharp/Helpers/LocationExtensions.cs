using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions;
using OmniSharp.Models;

namespace OmniSharp.Helpers
{
    public static class LocationExtensions
    {
        public static QuickFix GetQuickFix(this Location location, OmniSharpWorkspace workspace)
        {
            if (!location.IsInSource)
                throw new Exception("Location is not in the source tree");

            var lineSpan = Path.GetExtension(location.SourceTree.FilePath).Equals(".cake", StringComparison.OrdinalIgnoreCase) ||
                location.SourceTree.FilePath.EndsWith("razor__virtual.cs") ||
                location.SourceTree.FilePath.EndsWith("cshtml__virtual.cs")
                ? location.GetLineSpan()
                : location.GetMappedLineSpan();

            var documents = workspace.GetDocuments(lineSpan.Path);
            var sourceText = GetSourceText(location, documents, lineSpan.HasMappedPath);
            var text = GetLineText(location, sourceText, lineSpan.StartLinePosition.Line);

            var generatedInfo = workspace.CurrentSolution.GetSourceGeneratedFileInfo(location);

            var fileName = Path.IsPathRooted(lineSpan.Path) || generatedInfo != null
                // If there is generated file information, the path is not rooted, but we don't want to try and locate it as it doesn't
                // exist on disk
                ? lineSpan.Path
                // when a #line directive maps into a separate file using a relative path, get the full path relative to the folder containing the source tree
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(location.SourceTree.FilePath), lineSpan.Path));


            return new SymbolLocation
            {
                Text = text.Trim(),
                FileName = fileName,
                Line = lineSpan.StartLinePosition.Line,
                Column = lineSpan.HasMappedPath ? 0 : lineSpan.StartLinePosition.Character, // when a #line directive maps into a separate file, assume columns (0,0)
                EndLine = lineSpan.EndLinePosition.Line,
                EndColumn = lineSpan.HasMappedPath ? 0 : lineSpan.EndLinePosition.Character,
                Projects = documents.Select(document => document.Project.Name).ToArray(),
                GeneratedFileInfo = generatedInfo
            };

            static SourceText GetSourceText(Location location, IEnumerable<Document> documents, bool hasMappedPath)
            {
                // if we have a mapped linespan and we found a corresponding document, pick that one
                // otherwise use the SourceText of current location
                if (hasMappedPath)
                {
                    SourceText source = null;
                    if (documents != null && documents.FirstOrDefault(d => d != null && d.TryGetText(out source)) != null)
                    {
                        // we have a mapped document that exists in workspace
                        return source;
                    }

                    // we have a mapped document that doesn't exist in workspace
                    // in that case we have to always fall back to original linespan
                    return null;
                }

                // unmapped document so just continue with current SourceText
                return location.SourceTree.GetText();
            }

            static string GetLineText(Location location, SourceText sourceText, int startLine)
            {
                // bounds check in case the mapping is incorrect, since user can put whatever line they want
                if (sourceText != null && sourceText.Lines.Count > startLine)
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
