using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;
using Microsoft.CodeAnalysis.CSharp.Symbols;


namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("findsymbols")]
        public async Task<QuickFixResponse> FindSymbols()
        {
            var symbols = await GetSymbols();
            return new QuickFixResponse(symbols);
        }

        private async Task<IEnumerable<QuickFix>> GetSymbols()
        {
            var projects = _workspace.CurrentSolution.Projects;

            var symbols = new List<QuickFix>();
            foreach (var project in projects)
            {
                var compilation = await project.GetCompilationAsync();
                symbols.AddRange(GetCompilationSymbols(compilation));
            }
            return symbols.Distinct();
        }

        private IEnumerable<QuickFix> GetCompilationSymbols(Compilation compilation)
        {
            var namespaces = GetAllNamespaces(compilation.Assembly.GlobalNamespace)
                                .Where(n => n.Locations.Any(loc => loc.IsInSource));

            return
                from name in namespaces
                from type in name.GetTypeMembers()
                from symbol in type.GetMembers()
                from location in symbol.Locations
                where symbol.CanBeReferencedByName
                select ConvertSymbol(symbol, location);
        }

        private IEnumerable<INamespaceSymbol> GetAllNamespaces(INamespaceSymbol namespaceSymbol)
        {
            yield return namespaceSymbol;
            foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                foreach (var subNamespace in GetAllNamespaces(childNamespace))
                {
                    yield return subNamespace;
                }
            }
        }

        private QuickFix ConvertSymbol(ISymbol symbol, Location location)
        {
            var lineSpan = location.GetLineSpan();
            var path = lineSpan.Path;
            var documents = _workspace.GetDocuments(path);

            return new QuickFix
            {
                Text = new SnippetGenerator().GenerateSnippet(symbol),
                FileName = path,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                EndLine = lineSpan.EndLinePosition.Line + 1,
                EndColumn = lineSpan.EndLinePosition.Character + 1,
                Projects = documents.Select(document => document.Project.Name).ToArray()
            };
        }
    }
}