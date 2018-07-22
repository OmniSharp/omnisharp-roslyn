using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Cake.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FindUsages;

namespace OmniSharp.Cake.Services.RequestHandlers.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.FindUsages, Constants.LanguageNames.Cake), Shared]
    public class FindUsagesHandler : CakeRequestHandler<FindUsagesRequest, QuickFixResponse>
    {
        [ImportingConstructor]
        public FindUsagesHandler(
            OmniSharpWorkspace workspace)
            : base(workspace)
        {
        }

        protected override async Task<QuickFixResponse> TranslateResponse(QuickFixResponse response, FindUsagesRequest request)
        {
            response = await response.TranslateAsync(Workspace, request, removeGenerated: true);

            return request.OnlyThisFile ?
                response.OnlyThisFile(request.FileName) :
                response;
        }
    }
}
