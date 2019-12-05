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
using Microsoft.Extensions.Logging;
using OmniSharp.Abstractions.Models.V1.FixAll;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Roslyn.Utilities;
using FixAllScope = OmniSharp.Abstractions.Models.V1.FixAll.FixAllScope;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.RunFixAll, LanguageNames.CSharp)]
    public class RunFixAllCodeActionService : FixAllCodeActionBase, IRequestHandler<RunFixAllRequest, RunFixAllResponse>
    {
        private readonly ILogger<RunFixAllCodeActionService> _logger;
        private readonly FixAllDiagnosticProvider _fixAllDiagnosticProvider;

        [ImportingConstructor]
        public RunFixAllCodeActionService(ICsDiagnosticWorker diagnosticWorker, CachingCodeFixProviderForProjects codeFixProvider, OmniSharpWorkspace workspace, ILoggerFactory loggerFactory) : base(diagnosticWorker, codeFixProvider, workspace)
        {
            _logger = loggerFactory.CreateLogger<RunFixAllCodeActionService>();
            _fixAllDiagnosticProvider = new FixAllDiagnosticProvider(diagnosticWorker);
        }

        public async Task<RunFixAllResponse> Handle(RunFixAllRequest request)
        {
            if(request.Scope != FixAllScope.Document && request.FixAllFilter == null)
                throw new NotImplementedException($"Only scope '{nameof(FixAllScope.Document)}' is currently supported when filter '{nameof(request.FixAllFilter)}' is not set.");

            var solutionBeforeChanges = Workspace.CurrentSolution;

            var mappedProvidersWithDiagnostics = await GetDiagnosticsMappedWithFixAllProviders(request.Scope, request.FileName);

            var filteredProvidersWithFix = mappedProvidersWithDiagnostics
                .Where(diagWithFix =>
                {
                    if (request.FixAllFilter == null)
                        return true;

                    return request.FixAllFilter.Any(x => diagWithFix.HasFixForId(x.Id));
                });

            var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(request.Timeout));

            foreach (var singleFixableProviderWithDocument in filteredProvidersWithFix)
            {
                try
                {
                    var document = Workspace.CurrentSolution.GetDocument(singleFixableProviderWithDocument.DocumentId);

                    var fixer = singleFixableProviderWithDocument.FixAllProvider;

                    var (action, fixableDiagnosticIds) = await singleFixableProviderWithDocument.RegisterCodeFixesOrDefault(document);

                    if (action == null)
                        continue;

                    var fixAllContext = new FixAllContext(
                        document,
                        singleFixableProviderWithDocument.CodeFixProvider,
                        Microsoft.CodeAnalysis.CodeFixes.FixAllScope.Project,
                        action.EquivalenceKey,
                        fixableDiagnosticIds,
                        _fixAllDiagnosticProvider,
                        cancellationSource.Token
                    );

                    var fixes = await singleFixableProviderWithDocument.FixAllProvider.GetFixAsync(fixAllContext);

                    if (fixes == null)
                        continue;

                    var operations = await fixes.GetOperationsAsync(cancellationSource.Token);

                    foreach (var o in operations)
                    {
                        _logger.LogInformation($"Applying operation {o.ToString()} from fix all with fix provider {singleFixableProviderWithDocument.CodeFixProvider} to workspace document {document.FilePath}.");

                        if (o is ApplyChangesOperation applyChangesOperation)
                        {
                            applyChangesOperation.Apply(Workspace, cancellationSource.Token);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Running fix all action {singleFixableProviderWithDocument} in document {singleFixableProviderWithDocument.DocumentPath} prevented by error: {ex}");
                }
            }

            var currentSolution = Workspace.CurrentSolution;

            if (request.ApplyTextChanges)
            {
                Workspace.TryApplyChanges(currentSolution);
            }

            var changes = await GetFileChangesAsync(Workspace.CurrentSolution, solutionBeforeChanges, Path.GetDirectoryName(request.FileName), true, true);

            return new RunFixAllResponse
            {
                Changes = changes.FileChanges
            };
        }

        private async Task<(Solution Solution, IEnumerable<FileOperationResponse> FileChanges)> GetFileChangesAsync(Solution newSolution, Solution oldSolution, string directory, bool wantTextChanges, bool wantsAllCodeActionOperations)
        {
            var solution = oldSolution;
            var filePathToResponseMap = new Dictionary<string, FileOperationResponse>();
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
                                _logger.LogError($"File already exists on disk: '{newFilePath}'");
                                break;
                            }
                        }

                        this.Workspace.AddDocument(documentId, projectChange.NewProject, newFilePath, newDocument.SourceCodeKind);
                        solution = this.Workspace.CurrentSolution;
                    }
                    else
                    {
                        // The file already exists in the workspace? We're in a bad state.
                        _logger.LogError($"File already exists in workspace: '{newFilePath}'");
                    }
                }

                // Handle changed documents
                foreach (var documentId in projectChange.GetChangedDocuments())
                {
                    var newDocument = newSolution.GetDocument(documentId);
                    var oldDocument = oldSolution.GetDocument(documentId);
                    var filePath = newDocument.FilePath;

                    // file rename
                    if (oldDocument != null && newDocument.Name != oldDocument.Name)
                    {
                        if (wantsAllCodeActionOperations)
                        {
                            var newFilePath = GetNewFilePath(newDocument.Name, oldDocument.FilePath);
                            var text = await oldDocument.GetTextAsync();
                            var temp = solution.RemoveDocument(documentId);
                            solution = temp.AddDocument(DocumentId.CreateNewId(oldDocument.Project.Id, newDocument.Name), newDocument.Name, text, oldDocument.Folders, newFilePath);

                            filePathToResponseMap[filePath] = new RenamedFileResponse(oldDocument.FilePath, newFilePath);
                            filePathToResponseMap[newFilePath] = new OpenFileResponse(newFilePath);
                        }
                        continue;
                    }

                    if (!filePathToResponseMap.TryGetValue(filePath, out var fileOperationResponse))
                    {
                        fileOperationResponse = new ModifiedFileResponse(filePath);
                        filePathToResponseMap[filePath] = fileOperationResponse;
                    }

                    if (fileOperationResponse is ModifiedFileResponse modifiedFileResponse)
                    {
                        if (wantTextChanges)
                        {
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
            }

            return (solution, filePathToResponseMap.Values);
        }

        private static string GetNewFilePath(string newFileName, string currentFilePath)
        {
            var directory = Path.GetDirectoryName(currentFilePath);
            return Path.Combine(directory, newFileName);
        }

        private class FixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
        {
            private readonly ICsDiagnosticWorker _diagnosticWorker;

            public FixAllDiagnosticProvider(ICsDiagnosticWorker diagnosticWorker)
            {
                _diagnosticWorker = diagnosticWorker;
            }

            public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                var diagnostics = await _diagnosticWorker.GetDiagnostics(project.Documents.Select(x => x.FilePath).ToImmutableArray());
                return diagnostics.SelectMany(x => x.Diagnostics);
            }

            public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
            {
                var documentDiagnostics = await _diagnosticWorker.GetDiagnostics(ImmutableArray.Create(document.FilePath));

                if (!documentDiagnostics.Any())
                    return new Diagnostic[] { };

                return documentDiagnostics.First().Diagnostics;
            }

            public override async Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                var diagnostics = await _diagnosticWorker.GetDiagnostics(project.Documents.Select(x => x.FilePath).ToImmutableArray());
                return diagnostics.SelectMany(x => x.Diagnostics);
            }
        }
    }
}
