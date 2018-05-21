using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Cake.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.MembersTree;
using OmniSharp.Models.Rename;

namespace OmniSharp.Cake.Services.RequestHandlers.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.Rename, Constants.LanguageNames.Cake), Shared]
    public class RenameHandler : CakeRequestHandler<RenameRequest, RenameResponse>
    {
        [ImportingConstructor]
        public RenameHandler(OmniSharpWorkspace workspace) : base(workspace)
        {
        }

        protected override Task<RenameResponse> TranslateResponse(RenameResponse response, RenameRequest request)
        {
            return response.TranslateAsync(Workspace, request);
        }
    }
}
