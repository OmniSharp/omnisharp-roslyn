﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.V2.CodeActions;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Services;
using OmniSharp.Utilities;
using FixAllScope = OmniSharp.Abstractions.Models.V1.FixAll.FixAllScope;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    public abstract class BaseCodeActionService<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    {
        protected readonly OmniSharpWorkspace Workspace;
        protected readonly IEnumerable<ICodeActionProvider> Providers;
        protected readonly ILogger Logger;
        private readonly ICsDiagnosticWorker diagnostics;
        private readonly CachingCodeFixProviderForProjects codeFixesForProject;
        private readonly MethodInfo _getNestedCodeActions;

        protected Lazy<List<CodeRefactoringProvider>> OrderedCodeRefactoringProviders;

        // CS8019 isn't directly used (via roslyn) but has an analyzer that report different diagnostic based on CS8019 to improve user experience.
        private readonly Dictionary<string, string> customDiagVsFixMap = new Dictionary<string, string>
        {
            { "CS8019", "RemoveUnnecessaryImportsFixable" }
        };

        protected BaseCodeActionService(
            OmniSharpWorkspace workspace,
            IEnumerable<ICodeActionProvider> providers,
            ILogger logger,
            ICsDiagnosticWorker diagnostics,
            CachingCodeFixProviderForProjects codeFixesForProject)
        {
            Workspace = workspace;
            Providers = providers;
            Logger = logger;
            this.diagnostics = diagnostics;
            this.codeFixesForProject = codeFixesForProject;
            OrderedCodeRefactoringProviders = new Lazy<List<CodeRefactoringProvider>>(() => GetSortedCodeRefactoringProviders());

            // Sadly, the CodeAction.NestedCodeActions property is still internal.
            var nestedCodeActionsProperty = typeof(CodeAction).GetProperty("NestedCodeActions", BindingFlags.NonPublic | BindingFlags.Instance);
            if (nestedCodeActionsProperty == null)
            {
                throw new InvalidOperationException("Could not find CodeAction.NestedCodeActions property.");
            }

            this._getNestedCodeActions = nestedCodeActionsProperty.GetGetMethod(nonPublic: true);

            if (this._getNestedCodeActions == null)
            {
                throw new InvalidOperationException("Could not retrieve 'get' method for CodeAction.NestedCodeActions property.");
            }
        }

        public abstract Task<TResponse> Handle(TRequest request);

        protected async Task<IEnumerable<AvailableCodeAction>> GetAvailableCodeActions(ICodeActionRequest request)
        {
            // To produce a complete list of code actions for the document wait until all projects are loaded.
            var document = await this.Workspace.GetDocumentFromFullProjectModelAsync(request.FileName);
            if (document == null)
            {
                return Array.Empty<AvailableCodeAction>();
            }

            var codeActions = new List<CodeAction>();

            var sourceText = await document.GetTextAsync();
            var span = GetTextSpan(request, sourceText);

            await CollectCodeFixesActions(document, span, codeActions);
            await CollectRefactoringActions(document, span, codeActions);

            var distinctActions = codeActions.GroupBy(x => x.Title).Select(x => x.First());

            var availableActions = ConvertToAvailableCodeAction(distinctActions);

            return FilterBlacklistedCodeActions(availableActions);
        }

        private static IEnumerable<AvailableCodeAction> FilterBlacklistedCodeActions(IEnumerable<AvailableCodeAction> codeActions)
        {
            // Most of actions with UI works fine with defaults, however there's few exceptions:
            return codeActions.Where(x =>
            {
                var actionName = x.CodeAction.GetType().Name;

                return actionName != "GenerateTypeCodeActionWithOption" &&         // Blacklisted because doesn't give additional value over non UI generate type (when defaults used.)
                        actionName != "ChangeSignatureCodeAction" &&                // Blacklisted because cannot be used without proper UI.
                        actionName != "PullMemberUpWithDialogCodeAction";           // Blacklisted because doesn't give additional value over non UI generate type (when defaults used.)
            });
        }

        private TextSpan GetTextSpan(ICodeActionRequest request, SourceText sourceText)
        {
            if (request.Selection != null)
            {
                return sourceText.GetSpanFromRange(request.Selection);
            }

            var position = sourceText.GetPositionFromLineAndOffset(request.Line, request.Column);
            return new TextSpan(position, length: 0);
        }

        private async Task CollectCodeFixesActions(Document document, TextSpan span, List<CodeAction> codeActions)
        {
            var diagnosticsWithProjects = await diagnostics.GetDiagnostics(ImmutableArray.Create(document.FilePath));

            var groupedBySpan = diagnosticsWithProjects
                    .SelectMany(x => x.Diagnostics)
                    .Where(diagnostic => span.IntersectsWith(diagnostic.Location.SourceSpan))
                    .GroupBy(diagnostic => diagnostic.Location.SourceSpan);

            foreach (var diagnosticGroupedBySpan in groupedBySpan)
            {
                var diagnosticSpan = diagnosticGroupedBySpan.Key;
                var diagnosticsWithSameSpan = diagnosticGroupedBySpan.OrderByDescending(d => d.Severity);

                await AppendFixesAsync(document, diagnosticSpan, diagnosticsWithSameSpan, codeActions);
            }
        }

        private async Task AppendFixesAsync(Document document, TextSpan span, IEnumerable<Diagnostic> diagnostics, List<CodeAction> codeActions)
        {
            foreach (var codeFixProvider in GetSortedCodeFixProviders(document))
            {
                var fixableDiagnostics = diagnostics.Where(d => HasFix(codeFixProvider, d.Id)).ToImmutableArray();

                if (fixableDiagnostics.Length > 0)
                {
                    var context = new CodeFixContext(document, span, fixableDiagnostics, (a, _) => codeActions.Add(a), CancellationToken.None);

                    try
                    {
                        await codeFixProvider.RegisterCodeFixesAsync(context);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError(ex, $"Error registering code fixes for {codeFixProvider.GetType().FullName}");
                    }
                }
            }
        }

        private List<CodeFixProvider> GetSortedCodeFixProviders(Document document)
        {
            return ExtensionOrderer.GetOrderedOrUnorderedList<CodeFixProvider, ExportCodeFixProviderAttribute>(codeFixesForProject.GetAllCodeFixesForProject(document.Project.Id), attribute => attribute.Name).ToList();
        }

        private List<CodeRefactoringProvider> GetSortedCodeRefactoringProviders()
        {
            var providerList = this.Providers.SelectMany(provider => provider.CodeRefactoringProviders);
            return ExtensionOrderer.GetOrderedOrUnorderedList<CodeRefactoringProvider, ExportCodeFixProviderAttribute>(providerList, attribute => attribute.Name).ToList();
        }

        private bool HasFix(CodeFixProvider codeFixProvider, string diagnosticId)
        {
            return codeFixProvider.FixableDiagnosticIds.Any(id => id == diagnosticId)
                || (customDiagVsFixMap.ContainsKey(diagnosticId) && codeFixProvider.FixableDiagnosticIds.Any(id => id == customDiagVsFixMap[diagnosticId]));
        }

        private async Task CollectRefactoringActions(Document document, TextSpan span, List<CodeAction> codeActions)
        {
            var availableRefactorings = OrderedCodeRefactoringProviders.Value;

            foreach (var codeRefactoringProvider in availableRefactorings)
            {
                try
                {
                    var context = new CodeRefactoringContext(document, span, a => codeActions.Add(a), CancellationToken.None);
                    await codeRefactoringProvider.ComputeRefactoringsAsync(context);
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, $"Error computing refactorings for {codeRefactoringProvider.GetType().FullName}");
                }
            }
        }

        private IEnumerable<AvailableCodeAction> ConvertToAvailableCodeAction(IEnumerable<CodeAction> actions)
        {
            return actions.SelectMany(action =>
            {
                var nestedActions = this._getNestedCodeActions.Invoke<ImmutableArray<CodeAction>>(action, null);

                if (nestedActions.Any())
                {
                    return nestedActions.Select(nestedAction => new AvailableCodeAction(nestedAction, action));
                }

                return new[] { new AvailableCodeAction(action) };
            });
        }

        // Mapping means: each mapped item has one document that has one code fix provider and it's corresponding diagnostics.
        // If same document has multiple codefixers (diagnostics with different fixers) will them be mapped as separate items.
        protected async Task<ImmutableArray<DocumentWithFixProvidersAndMatchingDiagnostics>> GetDiagnosticsMappedWithFixAllProviders(FixAllScope scope, string fileName)
        {
            ImmutableArray<DocumentDiagnostics> allDiagnostics = await GetCorrectDiagnosticsInScope(scope, fileName);

            var mappedProvidersWithDiagnostics = allDiagnostics
                .SelectMany(diagnosticsInDocument =>
                    DocumentWithFixProvidersAndMatchingDiagnostics.CreateWithMatchingProviders(codeFixesForProject.GetAllCodeFixesForProject(diagnosticsInDocument.ProjectId), diagnosticsInDocument));

            return mappedProvidersWithDiagnostics.ToImmutableArray();
        }

        private async Task<ImmutableArray<DocumentDiagnostics>> GetCorrectDiagnosticsInScope(FixAllScope scope, string fileName)
        {
            switch (scope)
            {
                case FixAllScope.Solution:
                    var documentsInSolution = Workspace.CurrentSolution.Projects.SelectMany(x => x.Documents).Select(x => x.FilePath).ToImmutableArray();
                    return await diagnostics.GetDiagnostics(documentsInSolution);
                case FixAllScope.Project:
                    var documentsInProject = Workspace.GetDocument(fileName).Project.Documents.Select(x => x.FilePath).ToImmutableArray();
                    return await diagnostics.GetDiagnostics(documentsInProject);
                case FixAllScope.Document:
                    return await diagnostics.GetDiagnostics(ImmutableArray.Create(fileName));
                default:
                    throw new NotImplementedException();
            }
        }

        protected async Task<(Solution Solution, IEnumerable<FileOperationResponse> FileChanges)> GetFileChangesAsync(Solution newSolution, Solution oldSolution, string directory, bool wantTextChanges, bool wantsAllCodeActionOperations)
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
                                Logger.LogError($"File already exists on disk: '{newFilePath}'");
                                break;
                            }
                        }

                        this.Workspace.AddDocument(documentId, projectChange.NewProject, newFilePath, newDocument.SourceCodeKind);
                        solution = this.Workspace.CurrentSolution;
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
    }
}
