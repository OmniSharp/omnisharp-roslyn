using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Services.CodeActions;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.GetCodeActions, LanguageNames.CSharp)]
    public class GetCodeActionsService : BaseCodeActionService<GetCodeActionsRequest, GetCodeActionsResponse>
    {
        [ImportingConstructor]
        public GetCodeActionsService(
            OmniSharpWorkspace workspace,
            CodeActionHelper helper,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory)
            : base(workspace, helper, providers, loggerFactory.CreateLogger<GetCodeActionsService>())
        {
        }

        public override async Task<GetCodeActionsResponse> Handle(GetCodeActionsRequest request)
        {
            var availableActions = await GetAvailableCodeActions(request);

            return new GetCodeActionsResponse
            {
                CodeActions = availableActions.Select(ConvertToOmniSharpCodeAction)
            };
        }

        private static OmniSharpCodeAction ConvertToOmniSharpCodeAction(AvailableCodeAction availableAction)
        {
            return new OmniSharpCodeAction(availableAction.GetIdentifier(), availableAction.GetTitle());
        }
    }
}
