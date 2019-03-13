using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Helpers;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Roslyn;

namespace OmniSharp.Workers.Diagnostics
{
    [Export, Shared]
    public class CSharpDiagnosticService
    {
        private readonly ILogger _logger;
        private readonly OmniSharpWorkspace _workspace;
        private readonly object _lock = new object();
        private readonly DiagnosticEventForwarder _forwarder;
        private readonly IObserver<string> _openDocuments;

        [ImportingConstructor]
        public CSharpDiagnosticService(OmniSharpWorkspace workspace, DiagnosticEventForwarder forwarder, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _forwarder = forwarder;
            _logger = loggerFactory.CreateLogger<CSharpDiagnosticService>();

            var openDocumentsSubject = new Subject<string>();
            _openDocuments = openDocumentsSubject;

            _workspace.WorkspaceChanged += OnWorkspaceChanged;
            _workspace.DocumentOpened += OnDocumentOpened;
            _workspace.DocumentClosed += OnDocumentOpened;

            openDocumentsSubject
                .GroupByUntil(x => true, group => Observable.Amb(
                    group.Throttle(TimeSpan.FromMilliseconds(200)),
                    group.Distinct().Skip(99))
                )
                .Select(x => x.ToArray())
                .Merge()
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

            EmitDiagnostics(args.Document.FilePath);
            EmitDiagnostics(_workspace.GetOpenDocumentIds().Select(x => _workspace.CurrentSolution.GetDocument(x).FilePath).ToArray());
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

                EmitDiagnostics(newDocument.FilePath);
                EmitDiagnostics(_workspace.GetOpenDocumentIds().Select(x => _workspace.CurrentSolution.GetDocument(x).FilePath).ToArray());
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
            var items = await documents.FindDiagnosticLocationsAsync(_workspace);

            return new DiagnosticResult()
            {
                FileName = filePath,
                QuickFixes = items
            };
        }
    }
}
