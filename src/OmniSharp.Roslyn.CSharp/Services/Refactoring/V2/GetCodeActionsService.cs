using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Extensions;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    [OmniSharpHandler(OmnisharpEndpoints.V2.GetCodeActions, LanguageNames.CSharp)]
    public class GetCodeActionsService : RequestHandler<GetCodeActionsRequest, GetCodeActionsResponse>
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _codeActionProviders;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public GetCodeActionsService(OmnisharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _codeActionProviders = providers;
            _logger = loggerFactory.CreateLogger<GetCodeActionsService>();
        }

        public async Task<GetCodeActionsResponse> Handle(GetCodeActionsRequest request)
        {
            var actions = await CodeActionHelper.GetActions(_workspace, _codeActionProviders, _logger, request);
            return new GetCodeActionsResponse { CodeActions = actions.Select(a => new OmniSharpCodeAction(a.GetIdentifier(), a.Title)) };
        }
    }
}
