using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Mef;
using OmniSharp.Cake.Extensions;
using OmniSharp.Models.GotoDefinition;

namespace OmniSharp.Cake.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.GotoDefinition, Constants.LanguageNames.Cake), Shared]
    public class GotoDefinitionHandler : CakeRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>
    {
        [ImportingConstructor]
        public GotoDefinitionHandler(
            OmniSharpWorkspace workspace)
            : base(workspace)
        {
        }

        protected override Task<GotoDefinitionResponse> TranslateResponse(GotoDefinitionResponse response, GotoDefinitionRequest request)
        {
            return response.TranslateAsync(Workspace);
        }
    }
}