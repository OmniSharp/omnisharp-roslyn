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
        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();

        [ImportingConstructor]
        public DocumentDiagnosticService(OmnisharpWorkspace workspace, DiagnosticEventForwarder forwarder)
        {
            _workspace = workspace;
            _forwarder = forwarder;
        }

        public void EmitDiagnostics(params string[] filePaths)
        {
            if (_forwarder.IsEnabled)
            {
                foreach (var filePath in filePaths)
                {
                    if (!_queue.Contains(filePath))
                    {
                        _queue.Enqueue(filePath);
                    }
                }

                if (!_queueRunning && !_queue.IsEmpty)
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
                var concurrentTasks = 0;
                int remainingTime = 0;

                // Overall goal is to complete the queue within 10 seconds
                do
                {
                    concurrentTasks++;
                    remainingTime = (_queue.Count * 100) / concurrentTasks;
                } while (remainingTime > 1000);

                concurrentTasks = Math.Min(concurrentTasks, 4);

                var tasks = new List<Task>();
                for (var i = 0; i < concurrentTasks; i++)
                {
                    string filePath = null;
                    if (_queue.TryDequeue(out filePath))
                    {
                        tasks.Add(this.ProcessNextItem(filePath));
                    }
                    if (_queue.IsEmpty) break;
                }

                await Task.WhenAll(tasks.ToArray());

                if (_queue.IsEmpty)
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

        private async Task ProcessNextItem(string filePath)
        {
            var documents = _workspace.GetDocuments(filePath);

            if (documents.Any())
            {
                var quickFixes = new List<DiagnosticMessage>();
                quickFixes.Add(new DiagnosticMessage()
                {
                    Clear = true,
                    FileName = filePath
                });

                foreach (var document in documents)
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
                        var existingQuickFix = quickFixes.FirstOrDefault(q => q.Equals(quickFix));
                        if (existingQuickFix == null)
                        {
                            quickFix.Projects.Add(document.Project.Name);
                            quickFixes.Add(quickFix);
                        }
                        else
                        {
                            existingQuickFix.Projects.Add(document.Project.Name);
                        }
                    }
                }

                foreach (var quickFix in quickFixes)
                {
                    _forwarder.Forward(quickFix);
                }
            }
        }

        private static DiagnosticMessage MakeQuickFix(Diagnostic diagnostic)
        {
            var span = diagnostic.Location.GetMappedLineSpan();
            return new DiagnosticMessage
            {
                FileName = span.Path,
                Line = span.StartLinePosition.Line + 1,
                Column = span.StartLinePosition.Character + 1,
                EndLine = span.EndLinePosition.Line + 1,
                EndColumn = span.EndLinePosition.Character + 1,
                Text = diagnostic.GetMessage(),
                LogLevel = diagnostic.Severity.ToString()
            };
        }
    }
}
