using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Cake.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FindImplementations;

namespace OmniSharp.Cake.Services.RequestHandlers.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.FindImplementations, Constants.LanguageNames.Cake), Shared]
    public class FindImplementationsHandler : CakeRequestHandler<FindImplementationsRequest, QuickFixResponse>
    {
        [ImportingConstructor]
        public FindImplementationsHandler(OmniSharpWorkspace workspace) 
            : base(workspace)
        {
        }

        protected override Task<QuickFixResponse> TranslateResponse(QuickFixResponse response, FindImplementationsRequest request)
        {
            return response.TranslateAsync(Workspace, request);
        }
    }
}
