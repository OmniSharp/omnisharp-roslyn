using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models.V2.CodeActions;
using OmniSharp.Roslyn.CSharp.Services.CodeActions;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.GetCodeActions, LanguageNames.CSharp)]
    public class GetCodeActionsService : BaseCodeActionService<GetCodeActionsRequest, GetCodeActionsResponse>
    {
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        [ImportingConstructor]
        public GetCodeActionsService(
            OmniSharpWorkspace workspace,
            CodeActionHelper helper,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            [ImportMany] IEnumerable<IProjectSystem> projectSystems,
            ILoggerFactory loggerFactory)
            : base(workspace, helper, providers, loggerFactory.CreateLogger<GetCodeActionsService>())
        {
            _projectSystems = projectSystems;
        }

        public override async Task<GetCodeActionsResponse> Handle(GetCodeActionsRequest request)
        {
            // Waiting until the document is fully formed in memory (for project systems that have this ability) 
            // helps to reduce chances of returning invalid list of code actions while compilation is still in progress.
            await _projectSystems.WaitForAllProjectsToLoadForFileAsync(request.FileName);

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
