using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Mef;
using OmniSharp.Models.V2.CodeActions;

namespace OmniSharp.Cake.Services.RequestHandlers.Refactoring.V2
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.GetCodeActions, Constants.LanguageNames.Cake), Shared]
    public class GetCodeActionsHandler : BaseCodeActionsHandler<GetCodeActionsRequest, GetCodeActionsResponse>
    {
        [ImportingConstructor]
        public GetCodeActionsHandler(
            OmniSharpWorkspace workspace) 
            : base(workspace)
        {
        }

        protected override Task<GetCodeActionsResponse> TranslateResponse(GetCodeActionsResponse response, GetCodeActionsRequest request)
        {
            if (response?.CodeActions == null)
            {
                return Task.FromResult(response);
            }

            // At this point, we remove the "Rename file to.." code actions as these will
            // return the buffer as known to Roslyn to the client. Currently we don't store
            // the buffer as seen by the client and it's next to impossible to reverse it.
            response.CodeActions = response.CodeActions
                .Where(x => !x.Identifier.StartsWith("Rename file to"))
                .ToList();

            return Task.FromResult(response);
        }
    }
}
