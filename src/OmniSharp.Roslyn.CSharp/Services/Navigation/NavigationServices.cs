using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models.Navigate;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.NavigateUp, LanguageNames.CSharp)]
    public class NavigateUpService : IRequestHandler<NavigateUpRequest, NavigateResponse>
    {
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public NavigateUpService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<NavigateResponse> Handle(NavigateUpRequest request)
        {
            return await NavigationHelpers.Navigate(_workspace, request, NavigationHelpers.IsCloserNodeUp);
        }
    }

    [OmniSharpHandler(OmniSharpEndpoints.NavigateDown, LanguageNames.CSharp)]
    public class NavigateDownService : IRequestHandler<NavigateDownRequest, NavigateResponse>
    {
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public NavigateDownService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<NavigateResponse> Handle(NavigateDownRequest request)
        {
            return await NavigationHelpers.Navigate(_workspace, request, NavigationHelpers.IsCloserNodeDown);
        }
    }
}
