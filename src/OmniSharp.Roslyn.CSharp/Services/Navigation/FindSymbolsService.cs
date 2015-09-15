using System;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using FuzzySearch;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpEndpoint(typeof(RequestHandler<FindSymbolsRequest, QuickFixResponse>), LanguageNames.CSharp)]
    public class FindSymbolsService : RequestHandler<FindSymbolsRequest, QuickFixResponse>
    {
        private OmnisharpWorkspace _workspace;
        private readonly Nuzzaldrin _fuzz;

        [ImportingConstructor]
        public FindSymbolsService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
            _fuzz = new Nuzzaldrin();
        }

        public async Task<QuickFixResponse> Handle(FindSymbolsRequest request = null)
        {
            var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
                _workspace.CurrentSolution,
                name => _fuzz.Score(name, request?.Filter) > 0,
                SymbolFilter.TypeAndMember);

            var quickFixes = symbols
                .SelectMany(symbol =>
                    symbol.Locations.Select(
                        location => ConvertSymbol(symbol, location)
                    )).Distinct();

            return new QuickFixResponse(quickFixes);
        }

        private QuickFix ConvertSymbol(ISymbol symbol, Location location)
        {
            var lineSpan = location.GetLineSpan();
            var path = lineSpan.Path;
            var documents = _workspace.GetDocuments(path);

            var format = SymbolDisplayFormat.MinimallyQualifiedFormat;
            format = format.WithMemberOptions(format.MemberOptions
                                              ^ SymbolDisplayMemberOptions.IncludeContainingType
                                              ^ SymbolDisplayMemberOptions.IncludeType);

            format = format.WithKindOptions(SymbolDisplayKindOptions.None);

            return new SymbolLocation
            {
                Text = symbol.ToDisplayString(format),
                Kind = symbol.GetKind(),
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
