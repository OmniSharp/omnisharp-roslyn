using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Helpers;
using OmniSharp.Models;

namespace OmniSharp
{
    [Export(typeof(RequestHandler<Request, NavigateResponse>))]
    public class NavigationUpService : RequestHandler<Request, NavigateResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public NavigationUpService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<NavigateResponse> Handle(Request request)
        {
            return await NavigationHelpers.Navigate(_workspace, request, NavigationHelpers.IsCloserNodeUp);
        }
    }

    [Export(typeof(RequestHandler<Request, NavigateResponse>))]
    public class NavigationDownService : RequestHandler<Request, NavigateResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public NavigationDownService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<NavigateResponse> Handle(Request request)
        {
            return await NavigationHelpers.Navigate(_workspace, request, NavigationHelpers.IsCloserNodeDown);
        }
    }
}
