using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Extensions;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    [OmniSharpHandler(OmnisharpEndpoints.V2.RunCodeAction, LanguageNames.CSharp)]
    public class RunCodeActionService : RequestHandler<RunCodeActionRequest, RunCodeActionResponse>
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _codeActionProviders;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public RunCodeActionService(OmnisharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _codeActionProviders = providers;
            _logger = loggerFactory.CreateLogger<RunCodeActionService>();
        }

        public async Task<RunCodeActionResponse> Handle(RunCodeActionRequest request)
        {
            var response = new RunCodeActionResponse();

            var actions = await CodeActionHelper.GetActions(_workspace, _codeActionProviders, _logger, request);
            var action = actions.FirstOrDefault(a => a.GetIdentifier().Equals(request.Identifier));
            if (action == null)
            {
                return new RunCodeActionResponse();
            }

            _logger.LogInformation("Applying " + action);
            var operations = await action.GetOperationsAsync(CancellationToken.None);

            var solution = _workspace.CurrentSolution;
            var changes = Enumerable.Empty<OmniSharp.Models.ModifiedFileResponse>();
            var directoryName = Path.GetDirectoryName(request.FileName);
            foreach (var o in operations)
            {
                var applyChangesOperation = o as ApplyChangesOperation;
                if (applyChangesOperation != null)
                {
                    changes = changes.Concat(await FileChanges.GetFileChangesAsync(applyChangesOperation.ChangedSolution, solution, directoryName, request.WantsTextChanges));
                    solution = applyChangesOperation.ChangedSolution;
                }
            }

            if (request.ApplyTextChanges)
            {
                _workspace.TryApplyChanges(solution);
            }

            response.Changes = changes;
            return response;
        }
    }
}
