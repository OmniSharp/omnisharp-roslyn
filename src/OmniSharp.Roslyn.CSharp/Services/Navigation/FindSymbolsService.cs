using System;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
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

            var csprojSymbols = await _workspace.CurrentSolution.FindSymbols(isMatch, ".csproj");
            var projectJsonSymbols = await _workspace.CurrentSolution.FindSymbols(isMatch, ".json");
            return new QuickFixResponse()
            {
                QuickFixes = csprojSymbols.QuickFixes.Concat(projectJsonSymbols.QuickFixes)
            };
        }
    }
}
