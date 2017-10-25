using System.Composition;
using OmniSharp.Mef;
using OmniSharp.Models.V2;

namespace OmniSharp.Cake.Services.RequestHandlers.Refactoring.V2
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.GetCodeActions, Constants.LanguageNames.Cake), Shared]
    public class GetCodeActionsHandler : CakeRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse>
    {
        [ImportingConstructor]
        public GetCodeActionsHandler(
            OmniSharpWorkspace workspace) 
            : base(workspace)
        {
        }
    }
}
