using System;
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

            var lineSpan = location.GetLineSpan();
            var path = lineSpan.Path;
            var documents = workspace.GetDocuments(path);

            var line = lineSpan.StartLinePosition.Line;
            var text = location.SourceTree.GetText().Lines[line].ToString();

            return new QuickFix
            {
                Text = text.Trim(),
                FileName = path,
                Line = line,
                Column = lineSpan.StartLinePosition.Character,
                EndLine = lineSpan.EndLinePosition.Line,
                EndColumn = lineSpan.EndLinePosition.Character,
                Projects = documents.Select(document => document.Project.Name).ToArray()
            };
        }
    }
}
