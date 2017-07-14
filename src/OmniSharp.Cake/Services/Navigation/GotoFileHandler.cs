using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Cake.Extensions;
using OmniSharp.Models.GotoFile;

namespace OmniSharp.Cake.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.GotoFile, Constants.LanguageNames.Cake), Shared]
    public class GotoFileHandler : CakeRequestHandler<GotoFileRequest, QuickFixResponse>
    {
        [ImportingConstructor]
        public GotoFileHandler(
            OmniSharpWorkspace workspace)
            : base(workspace)
        {
        }

        protected override Task<QuickFixResponse> TranslateResponse(QuickFixResponse response, GotoFileRequest request)
        {
            return response.TranslateAsync(Workspace, request);
        }
    }
}
