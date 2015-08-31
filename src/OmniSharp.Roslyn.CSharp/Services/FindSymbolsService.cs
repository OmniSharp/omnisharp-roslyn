using System;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp
{
    [OmniSharpEndpoint(typeof(RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>), LanguageNames.CSharp)]
    public class FindSymbolsService : RequestHandler<FindSymbolsRequest, QuickFixResponse>
    {
        [Import]
        public OmnisharpWorkspace Workspace { get; set; }

        public async Task<QuickFixResponse> Handle(FindSymbolsRequest request = null)
        {
            Func<string, bool> isMatch =
                candidate => request != null
                ? candidate.IsValidCompletionFor(request.Filter)
                : true;

            return await FindSymbols(isMatch);
        }

        private async Task<QuickFixResponse> FindSymbols(Func<string, bool> predicate)
        {
            var symbols = await SymbolFinder.FindSourceDeclarationsAsync(Workspace.CurrentSolution, predicate, SymbolFilter.TypeAndMember);

            var quickFixes = (from symbol in symbols
                              from location in symbol.Locations
                              select ConvertSymbol(symbol, location)).Distinct();

            return new QuickFixResponse(quickFixes);
        }

        private QuickFix ConvertSymbol(ISymbol symbol, Location location)
        {
            var lineSpan = location.GetLineSpan();
            var path = lineSpan.Path;
            var documents = Workspace.GetDocuments(path);

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
