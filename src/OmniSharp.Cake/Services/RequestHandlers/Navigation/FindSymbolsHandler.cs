using System;
using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Cake.Extensions;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FindSymbols;
using static OmniSharp.Cake.Constants;

namespace OmniSharp.Cake.Services.RequestHandlers.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.FindSymbols, Constants.LanguageNames.Cake), Shared]
    public class FindSymbolsHandler : CakeRequestHandler<FindSymbolsRequest, QuickFixResponse>
    {
        [ImportingConstructor]
        public FindSymbolsHandler(OmniSharpWorkspace workspace)
            : base(workspace)
        {
        }

        public override Task<QuickFixResponse> Handle(FindSymbolsRequest request)
        {
            if (request != null && request.Filter != null && request.Filter.Length < request.MinFilterLength)
            {
                return Task.FromResult(new QuickFixResponse { QuickFixes = Array.Empty<QuickFix>() });
            }

            Func<string, bool> isMatch =
                candidate => request != null
                ? candidate.IsValidCompletionFor(request.Filter)
                : true;

            int maxItemsToReturn = request == null ? 0 : request.MaxItemsToReturn;
            return Workspace.CurrentSolution.FindSymbols(isMatch, LanguageNames.Cake, maxItemsToReturn);
        }

        protected override Task<QuickFixResponse> TranslateResponse(QuickFixResponse response, FindSymbolsRequest request)
        {
            return response.TranslateAsync(Workspace);
        }
    }
}
