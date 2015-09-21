using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Logging;
using OmniSharp.Mef;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Extensions;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    [OmniSharpHandler(typeof(RequestHandler<RunCodeActionRequest, RunCodeActionResponse>), LanguageNames.CSharp)]
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
            var actions = await CodeActionHelper.GetActions(_workspace, _codeActionProviders, _logger, request);

            var action = actions.FirstOrDefault(a => a.GetIdentifier().Equals(request.Identifier));
            if (action == null)
            {
                return new RunCodeActionResponse();
            }

            _logger.LogInformation("Applying " + action);
            var operations = await action.GetOperationsAsync(CancellationToken.None);

            var solution = _workspace.CurrentSolution;
            foreach (var o in operations)
            {
                o.Apply(_workspace, CancellationToken.None);
            }

            var response = new RunCodeActionResponse();
            var directoryName = Path.GetDirectoryName(request.FileName);
            var changes = await FileChanges.GetFileChangesAsync(_workspace.CurrentSolution, solution, directoryName, request.WantsTextChanges);

            response.Changes = changes;
            _workspace.TryApplyChanges(_workspace.CurrentSolution);
            return response;
        }
    }
}
