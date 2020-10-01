using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.CodeActions;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Services;
using RunCodeActionRequest = OmniSharp.Models.V2.CodeActions.RunCodeActionRequest;
using RunCodeActionResponse = OmniSharp.Models.V2.CodeActions.RunCodeActionResponse;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.RunCodeAction, LanguageNames.CSharp)]
    public class RunCodeActionService : BaseCodeActionService<RunCodeActionRequest, RunCodeActionResponse>
    {
        private readonly IAssemblyLoader _loader;
        private readonly Lazy<Assembly> _workspaceAssembly;

        [ImportingConstructor]
        public RunCodeActionService(
            IAssemblyLoader loader,
            OmniSharpWorkspace workspace,
            CodeActionHelper helper,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            ICsDiagnosticWorker diagnostics,
            CachingCodeFixProviderForProjects codeFixesForProjects)
            : base(workspace, providers, loggerFactory.CreateLogger<RunCodeActionService>(), diagnostics, codeFixesForProjects)
        {
            _loader = loader;
            _workspaceAssembly = _loader.LazyLoad(Configuration.RoslynWorkspaces);
        }

        public override async Task<RunCodeActionResponse> Handle(RunCodeActionRequest request)
        {
            var availableActions = await GetAvailableCodeActions(request);
            var availableAction = availableActions.FirstOrDefault(a => a.GetIdentifier().Equals(request.Identifier));
            if (availableAction == null)
            {
                return new RunCodeActionResponse();
            }

            Logger.LogInformation($"Applying code action: {availableAction.GetTitle()}");
            var changes = new List<FileOperationResponse>();

            try
            {
                var operations = await availableAction.GetOperationsAsync(CancellationToken.None);

                var solution = this.Workspace.CurrentSolution;
                var directory = Path.GetDirectoryName(request.FileName);

                foreach (var o in operations)
                {
                    if (o is ApplyChangesOperation applyChangesOperation)
                    {
                        var fileChangesResult = await GetFileChangesAsync(applyChangesOperation.ChangedSolution, solution, directory, request.WantsTextChanges, request.WantsAllCodeActionOperations);

                        changes.AddRange(fileChangesResult.FileChanges);
                        solution = fileChangesResult.Solution;
                    }
                    else
                    {
                        o.Apply(this.Workspace, CancellationToken.None);
                        solution = this.Workspace.CurrentSolution;
                    }

                    if (request.WantsAllCodeActionOperations)
                    {
                        if (o is OpenDocumentOperation openDocumentOperation)
                        {
                            var document = solution.GetDocument(openDocumentOperation.DocumentId);
                            changes.Add(new OpenFileResponse(document.FilePath));
                        }
                    }
                }

                if (request.ApplyTextChanges)
                {
                    // Will this fail if FileChanges.GetFileChangesAsync(...) added files to the workspace?
                    this.Workspace.TryApplyChanges(solution);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"An error occurred when running a code action: {availableAction.GetTitle()}");
            }

            return new RunCodeActionResponse
            {
                Changes = changes
            };
        }
    }
}
