using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("findsymbols")]
        public async Task<QuickFixResponse> FindSymbols()
        {
            Func<string, bool> isMatch = candidate => true;

            return await FindSymbols(isMatch);
        }

        [HttpPost("findsymbolswithfilter")]
        public async Task<QuickFixResponse> FindSymbols(FindSymbolsRequest request)
        {
            Func<string, bool> isMatch =
                candidate => candidate.IsValidCompletionFor(request.Filter);

            return await FindSymbols(isMatch);
        }

        private async Task<QuickFixResponse> FindSymbols(Func<string, bool> predicate)
        {
            var symbols = await SymbolFinder.FindSourceDeclarationsAsync(_workspace.CurrentSolution, predicate);

            var quickFixes = (from symbol in symbols
                              from location in symbol.Locations
                              where symbol.CanBeReferencedByName
                                 && symbol.Kind != SymbolKind.Namespace
                              select ConvertSymbol(symbol, location)).Distinct();

            return new QuickFixResponse(quickFixes);
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