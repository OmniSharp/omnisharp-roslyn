using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Cake.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.V2.CodeStructure;

namespace OmniSharp.Cake.Services.RequestHandlers.Structure
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.CodeStructure, Constants.LanguageNames.Cake), Shared]
    public class CodeStructureHandler : CakeRequestHandler<CodeStructureRequest, CodeStructureResponse>
    {
        [ImportingConstructor]
        public CodeStructureHandler(
            OmniSharpWorkspace workspace)
            : base(workspace)
        {
        }

        protected override Task<CodeStructureResponse> TranslateResponse(CodeStructureResponse response, CodeStructureRequest request)
        {
            return response.TranslateAsync(Workspace, request);
        }
    }
}
