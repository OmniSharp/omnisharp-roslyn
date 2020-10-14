
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Helpers;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    public class CSharpDiagnosticWorker: ICsDiagnosticWorker, IDisposable
    {
        private readonly ILogger _logger;
        private readonly OmniSharpWorkspace _workspace;
        private readonly DiagnosticEventForwarder _forwarder;
        private readonly IObserver<string> _openDocuments;
        private readonly IDisposable _disposable;

        public CSharpDiagnosticWorker(OmniSharpWorkspace workspace, DiagnosticEventForwarder forwarder, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _forwarder = forwarder;
            _logger = loggerFactory.CreateLogger<CSharpDiagnosticWorker>();

            var openDocumentsSubject = new Subject<string>();
            _openDocuments = openDocumentsSubject;

            _workspace.WorkspaceChanged += OnWorkspaceChanged;
            _workspace.DocumentOpened += OnDocumentOpened;
            _workspace.DocumentClosed += OnDocumentOpened;

            _disposable = openDocumentsSubject
                .Buffer(() => Observable.Amb(
                    openDocumentsSubject.Skip(99).Select(z => Unit.Default),
                    Observable.Timer(TimeSpan.FromMilliseconds(100)).Select(z => Unit.Default)
                ))
                .SubscribeOn(TaskPoolScheduler.Default)
                .Select(ProcessQueue)
                .Merge()
                .Subscribe();
        }

        private void OnDocumentOpened(object sender, DocumentEventArgs args)
        {
            if (!_forwarder.IsEnabled)
            {
                return;
            }
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs changeEvent)
        {
            if (!_forwarder.IsEnabled)
            {
                return;
            }

            if (changeEvent.Kind == WorkspaceChangeKind.DocumentAdded || changeEvent.Kind == WorkspaceChangeKind.DocumentChanged || changeEvent.Kind == WorkspaceChangeKind.DocumentReloaded)
            {
                var newDocument = changeEvent.NewSolution.GetDocument(changeEvent.DocumentId);

                EmitDiagnostics(new [] {newDocument.Id}.Union(_workspace.GetOpenDocumentIds()).Select(x => _workspace.CurrentSolution.GetDocument(x).FilePath).ToArray());
            }
            else if (changeEvent.Kind == WorkspaceChangeKind.ProjectAdded || changeEvent.Kind == WorkspaceChangeKind.ProjectReloaded)
            {
                EmitDiagnostics(changeEvent.NewSolution.GetProject(changeEvent.ProjectId).Documents.Select(x => x.FilePath).ToArray());
            }
        }

        public void QueueDiagnostics(params string[] documents)
        {
            if (!_forwarder.IsEnabled)
            {
                return;
            }

            this.EmitDiagnostics(documents);
        }

        private void EmitDiagnostics(params string[] documents)
        {
            if (!_forwarder.IsEnabled)
            {
                return;
            }

            foreach (var document in documents)
            {
                _openDocuments.OnNext(document);
            }
        }

        private IObservable<Unit> ProcessQueue(IEnumerable<string> filePaths)
        {
            return Observable.FromAsync(async () =>
            {
                var results = await Task.WhenAll(filePaths.Distinct().Select(ProcessNextItem));
                var message = new DiagnosticMessage()
                {
                    Results = results
                };

                _forwarder.Forward(message);
            });
        }

        private async Task<DiagnosticResult> ProcessNextItem(string filePath)
        {
            var documents = _workspace.GetDocuments(filePath);
            var semanticModels = await Task.WhenAll(documents.Select(doc => doc.GetSemanticModelAsync()));

            var items = semanticModels
                .SelectMany(sm => sm.GetDiagnostics());

            return new DiagnosticResult()
            {
                FileName = filePath,
                QuickFixes = items.Select(x => x.ToDiagnosticLocation()).Distinct().ToArray()
            };
        }

        public ImmutableArray<DocumentId> QueueForDiagnosis(ImmutableArray<string> documentPaths)
        {
            this.EmitDiagnostics(documentPaths.ToArray());
            return ImmutableArray<DocumentId>.Empty;
        }

        public async Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<string> documentPaths)
        {
            if (!documentPaths.Any()) return ImmutableArray<DocumentDiagnostics>.Empty;

            var results = new List<DocumentDiagnostics>();

            var documents =
                (await Task.WhenAll(
                    documentPaths
                        .Select(docPath => _workspace.GetDocumentsFromFullProjectModelAsync(docPath)))
                ).SelectMany(s => s);

            foreach (var document in documents)
            {
                if(document?.Project?.Name == null)
                    continue;

                var projectName = document.Project.Name;
                var diagnostics = await GetDiagnosticsForDocument(document, projectName);
                results.Add(new DocumentDiagnostics(document.Id, document.FilePath, document.Project.Id, document.Project.Name, diagnostics));
            }

            return results.ToImmutableArray();
        }

        private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsForDocument(Document document, string projectName)
        {
            // Only basic syntax check is available if file is miscellanous like orphan .cs file.
            // Those projects are on hard coded virtual project
            if (projectName == $"{Configuration.OmniSharpMiscProjectName}.csproj")
            {
                var syntaxTree = await document.GetSyntaxTreeAsync();
                return syntaxTree.GetDiagnostics().ToImmutableArray();
            }
            else
            {
                var semanticModel = await document.GetSemanticModelAsync();
                return semanticModel.GetDiagnostics();
            }
        }

        public ImmutableArray<DocumentId> QueueDocumentsForDiagnostics()
        {
            var documents = _workspace.CurrentSolution.Projects.SelectMany(x => x.Documents);
            QueueForDiagnosis(documents.Select(x => x.FilePath).ToImmutableArray());
            return documents.Select(x => x.Id).ToImmutableArray();
        }

        public ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(ImmutableArray<ProjectId> projectIds)
        {
            var documents = projectIds.SelectMany(projectId => _workspace.CurrentSolution.GetProject(projectId).Documents);
            QueueForDiagnosis(documents.Select(x => x.FilePath).ToImmutableArray());
            return documents.Select(x => x.Id).ToImmutableArray();
        }

        public Task<ImmutableArray<DocumentDiagnostics>> GetAllDiagnosticsAsync()
        {
            var documents = _workspace.CurrentSolution.Projects.SelectMany(x => x.Documents).Select(x => x.FilePath).ToImmutableArray();
            return GetDiagnostics(documents);
        }

        public void Dispose()
        {
            _workspace.WorkspaceChanged -= OnWorkspaceChanged;
            _workspace.DocumentOpened -= OnDocumentOpened;
            _workspace.DocumentClosed -= OnDocumentOpened;
            _disposable.Dispose();
        }

        public async Task<IEnumerable<Diagnostic>> AnalyzeDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await GetDiagnosticsForDocument(document, document.Project.Name);
        }

        public async Task<IEnumerable<Diagnostic>> AnalyzeProjectsAsync(Project project, CancellationToken cancellationToken)
        {
            var diagnostics = new List<Diagnostic>();
            foreach (var document in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                diagnostics.AddRange(await GetDiagnosticsForDocument(document, project.Name));
            }

            return diagnostics;
        }
    }
}
