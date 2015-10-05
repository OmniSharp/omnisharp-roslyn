using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmnisharpEndpoints.NavigateUp, LanguageNames.CSharp)]
    public class NavigateUpService : RequestHandler<NavigateUpRequest, NavigateResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public NavigateUpService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<NavigateResponse> Handle(NavigateUpRequest request)
        {
            return await NavigationHelpers.Navigate(_workspace, request, NavigationHelpers.IsCloserNodeUp);
        }
    }

    [OmniSharpHandler(OmnisharpEndpoints.NavigateDown, LanguageNames.CSharp)]
    public class NavigateDownService : RequestHandler<NavigateDownRequest, NavigateResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public NavigateDownService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<NavigateResponse> Handle(NavigateDownRequest request)
        {
            return await NavigationHelpers.Navigate(_workspace, request, NavigationHelpers.IsCloserNodeDown);
        }
    }
}
