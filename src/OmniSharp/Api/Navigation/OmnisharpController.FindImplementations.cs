using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("findimplementations")]
        public async Task<QuickFixResponse> FindImplementations(Request request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var response = new QuickFixResponse();

            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);
                var quickFixes = new List<QuickFix>();

                var implementations = await SymbolFinder.FindImplementationsAsync(symbol, _workspace.CurrentSolution);
                await AddQuickFixes(quickFixes, implementations);
                var overrides = await SymbolFinder.FindOverridesAsync(symbol, _workspace.CurrentSolution);
                await AddQuickFixes(quickFixes, overrides);

                var derivedTypes = await GetDerivedTypes(symbol);
                await AddQuickFixes(quickFixes, derivedTypes);

                response = new QuickFixResponse(quickFixes.OrderBy(q => q.FileName)
                                                            .ThenBy(q => q.Line)
                                                            .ThenBy(q => q.Column));
            }

            return response;
        }

        private async Task AddQuickFixes(ICollection<QuickFix> quickFixes, IEnumerable<ISymbol> symbols)
        {
            foreach (var symbol in symbols)
            {
                foreach (var location in symbol.Locations)
                {
                    await AddQuickFix(quickFixes, location);
                }
            }
        }

        private async Task<IEnumerable<ISymbol>> GetDerivedTypes(ISymbol typeSymbol)
        {
            var derivedTypes = new List<INamedTypeSymbol>();
            if (typeSymbol is INamedTypeSymbol)
            {
                var projects = _workspace.CurrentSolution.Projects;
                foreach (var project in projects)
                {
                    var compilation = await project.GetCompilationAsync();
                    var types = compilation.GlobalNamespace.GetTypeMembers();
                    foreach (var type in types)
                    {
                        if (GetBaseTypes(type).Contains(typeSymbol))
                        {
                            derivedTypes.Add(type);
                        }
                    }
                }
            }
            return derivedTypes;
        }

        private IEnumerable<INamedTypeSymbol> GetBaseTypes(ITypeSymbol type)
        {
            var current = type.BaseType;
            while (current != null)
            {
                yield return current;
                current = current.BaseType;
            }
        }
    }
}