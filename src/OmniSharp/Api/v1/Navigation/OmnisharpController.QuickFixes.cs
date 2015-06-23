using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        private async Task<QuickFix> GetQuickFix(Location location)
        {
            if (!location.IsInSource)
                throw new Exception("Location is not in the source tree");

            var lineSpan = location.GetLineSpan();
            var path = lineSpan.Path;
            var documents = _workspace.GetDocuments(path);
            var line = lineSpan.StartLinePosition.Line;
            var syntaxTree = await documents.First().GetSyntaxTreeAsync();
            var text = syntaxTree.GetText().Lines[line].ToString();

            return new QuickFix
            {
                Text = text.Trim(),
                FileName = path,
                Line = line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                EndLine = lineSpan.EndLinePosition.Line + 1,
                EndColumn = lineSpan.EndLinePosition.Character + 1,
                Projects = documents.Select(document => document.Project.Name).ToArray()
            };
        }

        private async Task AddQuickFix(ICollection<QuickFix> quickFixes, Location location)
        {
            if (location.IsInSource)
            {
                var quickFix = await GetQuickFix(location);
                quickFixes.Add(quickFix);
            }
        }
    }
}