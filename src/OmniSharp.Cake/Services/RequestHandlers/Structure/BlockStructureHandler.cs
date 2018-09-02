using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Cake.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.V2;

namespace OmniSharp.Cake.Services.RequestHandlers.Structure
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.BlockStructure, Constants.LanguageNames.Cake), Shared]
    public class BlockStructureHandler : CakeRequestHandler<BlockStructureRequest, BlockStructureResponse>
    {
        [ImportingConstructor]
        public BlockStructureHandler(OmniSharpWorkspace workspace) : base(workspace)
        {
        }

        protected override Task<BlockStructureResponse> TranslateResponse(BlockStructureResponse response, BlockStructureRequest request)
        {
            return response.TranslateAsync(Workspace, request);
        }
    }
}
