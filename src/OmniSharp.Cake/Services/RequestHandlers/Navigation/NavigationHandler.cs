using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Cake.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.Navigate;

namespace OmniSharp.Cake.Services.RequestHandlers.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.NavigateUp, Constants.LanguageNames.Cake), Shared]
    public class NavigateUpHandler : CakeRequestHandler<NavigateUpRequest, NavigateResponse>
    {
        [ImportingConstructor]
        public NavigateUpHandler(OmniSharpWorkspace workspace)
            : base(workspace)
        {
        }

        protected override Task<NavigateResponse> TranslateResponse(NavigateResponse response, NavigateUpRequest request)
        {
            return response.TranslateAsync(Workspace, request);
        }
    }

    [OmniSharpHandler(OmniSharpEndpoints.NavigateDown, Constants.LanguageNames.Cake), Shared]
    public class NavigateDownHandler : CakeRequestHandler<NavigateDownRequest, NavigateResponse>
    {
        [ImportingConstructor]
        public NavigateDownHandler(OmniSharpWorkspace workspace)
            : base(workspace)
        {
        }

        protected override Task<NavigateResponse> TranslateResponse(NavigateResponse response, NavigateDownRequest request)
        {
            return response.TranslateAsync(Workspace, request);
        }
    }
}
