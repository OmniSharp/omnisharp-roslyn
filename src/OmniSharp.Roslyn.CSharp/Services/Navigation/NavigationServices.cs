using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Helpers;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [Export(typeof(RequestHandler<Request, NavigateResponse>))]
    public class NavigateUpService : RequestHandler<Request, NavigateResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public NavigateUpService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<NavigateResponse> Handle(Request request)
        {
            return await NavigationHelpers.Navigate(_workspace, request, NavigationHelpers.IsCloserNodeUp);
        }
    }

    [Export(typeof(RequestHandler<Request, NavigateResponse>))]
    public class NavigateDownService : RequestHandler<Request, NavigateResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public NavigateDownService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<NavigateResponse> Handle(Request request)
        {
            return await NavigationHelpers.Navigate(_workspace, request, NavigationHelpers.IsCloserNodeDown);
        }
    }
}
