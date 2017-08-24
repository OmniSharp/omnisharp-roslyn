using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Cake.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.GotoDefinition;

namespace OmniSharp.Cake.Services.RequestHandlers.Navigation
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
            // TODO: If outside range, it's an alias. Then fetch metadata.
            return Task.FromResult(response);
        }
    }
}
