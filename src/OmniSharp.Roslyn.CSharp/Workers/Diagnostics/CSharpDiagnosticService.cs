using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
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
        private bool _queueRunning = false;
        private readonly ConcurrentQueue<string> _openDocuments = new ConcurrentQueue<string>();

        [ImportingConstructor]
        public CSharpDiagnosticService(OmniSharpWorkspace workspace, DiagnosticEventForwarder forwarder, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _forwarder = forwarder;
            _logger = loggerFactory.CreateLogger<CSharpDiagnosticService>();

            _workspace.WorkspaceChanged += OnWorkspaceChanged;
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs changeEvent)
        {
            if (changeEvent.Kind == WorkspaceChangeKind.DocumentChanged)
            {
                var newDocument = changeEvent.NewSolution.GetDocument(changeEvent.DocumentId);

                this.EmitDiagnostics(newDocument.FilePath);
                foreach (var document in _workspace.GetOpenDocumentIds().Select(x => _workspace.CurrentSolution.GetDocument(x)))
                {
                    this.EmitDiagnostics(document.FilePath);
                }
            }
        }

        public void QueueDiagnostics(params string[] documents)
        {
            this.EmitDiagnostics(documents);
        }

        private void EmitDiagnostics(params string[] documents)
        {
            if (_forwarder.IsEnabled)
            {
                foreach (var document in documents)
                {
                    if (!_openDocuments.Contains(document))
                    {
                        _openDocuments.Enqueue(document);
                    }
                }

                if (!_queueRunning && !_openDocuments.IsEmpty)
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
                await Dequeue();

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
            });
        }

        private async Task Dequeue()
        {
            var tasks = new List<Task<DiagnosticResult>>();
            for (var i = 0; i < 50; i++)
            {
                if (_openDocuments.IsEmpty)
                {
                    break;
                }

                if (_openDocuments.TryDequeue(out var filePath))
                {
                    tasks.Add(this.ProcessNextItem(filePath));
                }
            }

            if (!tasks.Any()) return;

            var diagnosticResults = await Task.WhenAll(tasks.ToArray());
            if (diagnosticResults.Any())
            {
                var message = new DiagnosticMessage()
                {
                    Results = diagnosticResults
                };

                this._forwarder.Forward(message);
            }

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
