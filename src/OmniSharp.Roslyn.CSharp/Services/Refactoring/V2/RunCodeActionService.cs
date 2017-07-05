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
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.CodeActions;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Services;

using RunCodeActionRequest = OmniSharp.Models.V2.RunCodeActionRequest;
using RunCodeActionResponse = OmniSharp.Models.V2.RunCodeActionResponse;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.RunCodeAction, LanguageNames.CSharp)]
    public class RunCodeActionService : BaseCodeActionService<RunCodeActionRequest, RunCodeActionResponse>
    {
        [ImportingConstructor]
        public RunCodeActionService(
            OmniSharpWorkspace workspace,
            CodeActionHelper helper,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory)
            : base(workspace, helper, providers, loggerFactory.CreateLogger<RunCodeActionService>())
        {
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

            var operations = await availableAction.GetOperationsAsync(CancellationToken.None);

            var solution = this.Workspace.CurrentSolution;
            var changes = new List<ModifiedFileResponse>();
            var directory = Path.GetDirectoryName(request.FileName);

            foreach (var o in operations)
            {
                if (o is ApplyChangesOperation applyChangesOperation)
                {
                    var fileChanges = await GetFileChangesAsync(applyChangesOperation.ChangedSolution, solution, directory, request.WantsTextChanges);

                    changes.AddRange(fileChanges);
                    solution = applyChangesOperation.ChangedSolution;
                }
            }

            if (request.ApplyTextChanges)
            {
                // Will this fail if FileChanges.GetFileChangesAsync(...) added files to the workspace?
                this.Workspace.TryApplyChanges(solution);
            }

            return new RunCodeActionResponse
            {
                Changes = changes
            };
        }

        private async Task<IEnumerable<ModifiedFileResponse>> GetFileChangesAsync(Solution newSolution, Solution oldSolution, string directory, bool wantTextChanges)
        {
            var filePathToResponseMap = new Dictionary<string, ModifiedFileResponse>();
            var solutionChanges = newSolution.GetChanges(oldSolution);

            foreach (var projectChange in solutionChanges.GetProjectChanges())
            {
                // Handle added documents
                foreach (var documentId in projectChange.GetAddedDocuments())
                {
                    var newDocument = newSolution.GetDocument(documentId);
                    var text = await newDocument.GetTextAsync();

                    var newFilePath = newDocument.FilePath == null || !Path.IsPathRooted(newDocument.FilePath)
                        ? Path.Combine(directory, newDocument.Name)
                        : newDocument.FilePath;

                    var modifiedFileResponse = new ModifiedFileResponse(newFilePath)
                    {
                        Changes = new[] {
                            new LinePositionSpanTextChange
                            {
                                NewText = text.ToString()
                            }
                        }
                    };

                    filePathToResponseMap[newFilePath] = modifiedFileResponse;

                    // We must add new files to the workspace to ensure that they're present when the host editor
                    // tries to modify them. This is a strange interaction because the workspace could be left
                    // in an incomplete state if the host editor doesn't apply changes to the new file, but it's
                    // what we've got today.
                    if (this.Workspace.GetDocument(newFilePath) == null)
                    {
                        var fileInfo = new FileInfo(newFilePath);
                        if (!fileInfo.Exists)
                        {
                            fileInfo.CreateText().Dispose();
                        }
                        else
                        {
                            // The file already exists on disk? Ensure that it's zero-length. If so, we can still use it.
                            if (fileInfo.Length > 0)
                            {
                                Logger.LogError($"File already exists on disk: '{newFilePath}'");
                                break;
                            }
                        }

                        this.Workspace.AddDocument(projectChange.ProjectId, newFilePath, newDocument.SourceCodeKind);
                    }
                    else
                    {
                        // The file already exists in the workspace? We're in a bad state.
                        Logger.LogError($"File already exists in workspace: '{newFilePath}'");
                    }
                }

                // Handle changed documents
                foreach (var documentId in projectChange.GetChangedDocuments())
                {
                    var newDocument = newSolution.GetDocument(documentId);
                    var filePath = newDocument.FilePath;

                    if (!filePathToResponseMap.TryGetValue(filePath, out var modifiedFileResponse))
                    {
                        modifiedFileResponse = new ModifiedFileResponse(filePath);
                        filePathToResponseMap[filePath] = modifiedFileResponse;
                    }

                    if (wantTextChanges)
                    {
                        var oldDocument = oldSolution.GetDocument(documentId);
                        var linePositionSpanTextChanges = await TextChanges.GetAsync(newDocument, oldDocument);

                        modifiedFileResponse.Changes = modifiedFileResponse.Changes != null
                            ? modifiedFileResponse.Changes.Union(linePositionSpanTextChanges)
                            : linePositionSpanTextChanges;
                    }
                    else
                    {
                        var text = await newDocument.GetTextAsync();
                        modifiedFileResponse.Buffer = text.ToString();
                    }
                }
            }

            return filePathToResponseMap.Values;
        }
    }
}
