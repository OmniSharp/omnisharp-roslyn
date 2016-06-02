using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Services;
using OmniSharp.Models;
using System.Collections.Concurrent;
using OmniSharp.Models.V2;

namespace OmniSharp.Roslyn
{
    [Export, Shared]
    public class DocumentDiagnosticService
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly object _lock = new object();
        private readonly DiagnosticEventForwarder _forwarder;
        private bool _queueRunning = false;
        private readonly ConcurrentQueue<DocumentId> _openDocuments = new ConcurrentQueue<DocumentId>();
        private readonly ConcurrentQueue<DocumentId> _backlog = new ConcurrentQueue<DocumentId>();

        [ImportingConstructor]
        public DocumentDiagnosticService(OmnisharpWorkspace workspace, DiagnosticEventForwarder forwarder)
        {
            _workspace = workspace;
            _forwarder = forwarder;

            _workspace.WorkspaceChanged += OnWorkspaceChanged;
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs changeEvent)
        {
            if (changeEvent.Kind == WorkspaceChangeKind.DocumentChanged)
            {
                if (_workspace.IsDocumentOpen(changeEvent.DocumentId))
                {
                    this.EmitDiagnostics(_openDocuments, changeEvent.DocumentId);
                }
                else
                {
                    _backlog.Enqueue(changeEvent.DocumentId);
                }
            }
        }

        public void QueueDiagnostics(params DocumentId[] documents)
        {
            this.EmitDiagnostics(_backlog, documents);
        }

        private void EmitDiagnostics(ConcurrentQueue<DocumentId> queue, params DocumentId[] documents)
        {
            if (_forwarder.IsEnabled)
            {
                foreach (var document in documents)
                {
                    if (!queue.Contains(document))
                    {
                        queue.Enqueue(document);
                    }
                }

                if (!_queueRunning && !queue.IsEmpty)
                {
                    this.ProcessQueue();
                }
            }
        }

        private void ProcessQueue()
        {
            lock (_lock)
            {
                _queueRunning = true;
            }

            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(100);
                await Dequeue(_openDocuments);
                await Dequeue(_backlog);

                if (_openDocuments.IsEmpty && _backlog.IsEmpty)
                {
                    lock (_lock)
                    {
                        _queueRunning = false;
                    }
                }
                else
                {
                    this.ProcessQueue();
                }
            });
        }

        private async Task Dequeue(ConcurrentQueue<DocumentId> queue)
        {
            var tasks = new List<Task<IEnumerable<DiagnosticLocation>>>();
            for (var i = 0; i < 50; i++)
            {
                if (queue.IsEmpty) break;
                DocumentId documentId = null;
                if (queue.TryDequeue(out documentId))
                {
                    tasks.Add(this.ProcessNextItem(documentId));
                }
            }

            var result = await Task.WhenAll(tasks.ToArray());

            var diagnosticResults = result
                .SelectMany(x => x)
                .GroupBy(x => x.FileName)
                .Select(x => new DiagnosticResult()
                {
                    FileName = x.Key,
                    QuickFixes = x.ToArray()
                });

            var message = new DiagnosticMessage()
            {
                Results = diagnosticResults
            };

            this._forwarder.Forward(message);

            if (_openDocuments.IsEmpty)
            {
                lock (_lock)
                {
                    _queueRunning = false;
                }
            }
            else
            {
                this.ProcessQueue();
            }
        }

        private async Task<IEnumerable<DiagnosticLocation>> ProcessNextItem(DocumentId documentId)
        {
            var document = _workspace.CurrentSolution.GetDocument(documentId);
            var items = new List<DiagnosticLocation>();

            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                IEnumerable<Diagnostic> diagnostics = semanticModel.GetDiagnostics();

                //script files can have custom directives such as #load which will be deemed invalid by Roslyn
                //we suppress the CS1024 diagnostic for script files for this reason. Roslyn will fix it later too, so this is temporary.
                if (document.SourceCodeKind != SourceCodeKind.Regular)
                {
                    diagnostics = diagnostics.Where(diagnostic => diagnostic.Id != "CS1024");
                }

                foreach (var quickFix in diagnostics.Select(MakeQuickFix))
                {
                    var existingQuickFix = items.FirstOrDefault(q => q.Equals(quickFix));
                    if (existingQuickFix == null)
                    {
                        quickFix.Projects.Add(document.Project.Name);
                        items.Add(quickFix);
                    }
                    else
                    {
                        existingQuickFix.Projects.Add(document.Project.Name);
                    }
                }
            }

            return items;
        }

        private static DiagnosticLocation MakeQuickFix(Diagnostic diagnostic)
        {
            var span = diagnostic.Location.GetMappedLineSpan();
            return new DiagnosticLocation
            {
                FileName = span.Path,
                Line = span.StartLinePosition.Line,
                Column = span.StartLinePosition.Character,
                EndLine = span.EndLinePosition.Line,
                EndColumn = span.EndLinePosition.Character,
                Text = diagnostic.GetMessage(),
                LogLevel = diagnostic.Severity.ToString()
            };
        }
    }
}