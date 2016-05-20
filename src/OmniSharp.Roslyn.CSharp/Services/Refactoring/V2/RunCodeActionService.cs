using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Extensions;
using OmniSharp.Roslyn.CSharp.Services.Testing;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    [OmniSharpHandler(OmnisharpEndpoints.V2.RunCodeAction, LanguageNames.CSharp)]
    public class RunCodeActionService : RequestHandler<RunCodeActionRequest, RunCodeActionResponse>
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _codeActionProviders;
        private readonly ILogger _logger;
        private readonly TestMethodsDiscover _testProvider;


        [ImportingConstructor]
        public RunCodeActionService(OmnisharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _codeActionProviders = providers;
            _logger = loggerFactory.CreateLogger<RunCodeActionService>();
            _testProvider = new TestMethodsDiscover(loggerFactory);
        }

        public Task<RunCodeActionResponse> Handle(RunCodeActionRequest request)
        {
            // Eventually this should be split into a seperate service
            var testRunner = _testProvider.GetTestActionRunner(request);
            if (testRunner != null)
            {
                return HandleTestActions(testRunner);
            }
            else
            {
                return HandleCodeActions(request);
            }
        }

        private async Task<RunCodeActionResponse> HandleTestActions(ITestActionRunner testRunner)
        {
            _logger.LogInformation($"run test action: [{testRunner}]");
            var result = await testRunner.RunAsync();
            
            return result.ToRespnse();
        }

        private async Task<RunCodeActionResponse> HandleCodeActions(RunCodeActionRequest request)
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
            foreach (var o in operations)
            {
                o.Apply(_workspace, CancellationToken.None);
            }

            var directoryName = Path.GetDirectoryName(request.FileName);
            var changes = await FileChanges.GetFileChangesAsync(_workspace.CurrentSolution, solution, directoryName, request.WantsTextChanges);

            response.Changes = changes;
            _workspace.TryApplyChanges(_workspace.CurrentSolution);
            return response;
        }
    }
}
