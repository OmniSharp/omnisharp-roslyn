using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Cake.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.V2.CodeActions;

namespace OmniSharp.Cake.Services.RequestHandlers.Refactoring.V2
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.RunCodeAction, Constants.LanguageNames.Cake), Shared]
    public class RunCodeActionsHandler : BaseCodeActionsHandler<RunCodeActionRequest, RunCodeActionResponse>
    {
        [ImportingConstructor]
        public RunCodeActionsHandler(
            OmniSharpWorkspace workspace)
            : base(workspace)
        {
        }

        protected override bool IsValid(RunCodeActionRequest request) => request.WantsTextChanges;

        protected override Task<RunCodeActionResponse> TranslateResponse(RunCodeActionResponse response, RunCodeActionRequest request)
        {
            return response.TranslateAsync(Workspace);
        }
    }
}
