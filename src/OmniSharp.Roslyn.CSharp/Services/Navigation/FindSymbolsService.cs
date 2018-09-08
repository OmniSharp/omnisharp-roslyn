using System;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FindSymbols;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.FindSymbols, LanguageNames.CSharp)]
    public class FindSymbolsService : IRequestHandler<FindSymbolsRequest, QuickFixResponse>
    {
        private OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public FindSymbolsService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<QuickFixResponse> Handle(FindSymbolsRequest request = null)
        {
            if (request != null && request.Filter != null && request.Filter.Length < request.MinFilterLength)
            {
                return new QuickFixResponse { QuickFixes = Array.Empty<QuickFix>() };
            }

            Func<string, bool> isMatch =
                candidate => request != null
                ? candidate.IsValidCompletionFor(request.Filter)
                : true;

            int maxItemsToReturn = request == null ? 0 : request.MaxItemsToReturn;
            var csprojSymbols = await _workspace.CurrentSolution.FindSymbols(isMatch, ".csproj", maxItemsToReturn);
            var projectJsonSymbols = await _workspace.CurrentSolution.FindSymbols(isMatch, ".json", maxItemsToReturn);
            return new QuickFixResponse()
            {
                QuickFixes = csprojSymbols.QuickFixes.Concat(projectJsonSymbols.QuickFixes)
            };
        }
    }
}
