using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FindSymbols;
using OmniSharp.Options;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.FindSymbols, LanguageNames.CSharp)]
    public class FindSymbolsService : IRequestHandler<FindSymbolsRequest, QuickFixResponse>
    {
        private OmniSharpWorkspace _workspace;
        private FindSymbolsOptions _options;

        [ImportingConstructor]
        public FindSymbolsService(OmniSharpWorkspace workspace, OmniSharpOptions omniSharpOptions)
        {
            _workspace = workspace;
            _options = omniSharpOptions.FindSymbols;
        }

        public async Task<QuickFixResponse> Handle(FindSymbolsRequest request = null)
        {
            if (request != null && request.Filter != null && request.Filter.Length < _options.MinFilterLength)
            {
                return new QuickFixResponse(new List<QuickFix>());
            }

            Func<string, bool> isMatch =
                candidate => request != null
                ? candidate.IsValidCompletionFor(request.Filter)
                : true;

            return await FindSymbols(isMatch);
        }

        private async Task<QuickFixResponse> FindSymbols(Func<string, bool> predicate)
        {
            var symbols = await SymbolFinder.FindSourceDeclarationsAsync(_workspace.CurrentSolution, predicate, SymbolFilter.TypeAndMember);

            var symbolLocations = new List<QuickFix>();
            foreach(var symbol in symbols)
            {
                // for partial methods, pick the one with body
                var s = symbol;
                if (s is IMethodSymbol method)
                {
                    s = method.PartialImplementationPart ?? symbol;
                }

                foreach (var location in s.Locations)
                {
                    symbolLocations.Add(ConvertSymbol(symbol, location));
                }

                if (_options.MaxItemsToReturn > 0 && symbolLocations.Count >= _options.MaxItemsToReturn)
                {
                    break;
                }
            }

            return new QuickFixResponse(symbolLocations.Distinct());
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
                Line = lineSpan.StartLinePosition.Line,
                Column = lineSpan.StartLinePosition.Character,
                EndLine = lineSpan.EndLinePosition.Line,
                EndColumn = lineSpan.EndLinePosition.Character,
                Projects = documents.Select(document => document.Project.Name).ToArray()
            };
        }

    }
}
