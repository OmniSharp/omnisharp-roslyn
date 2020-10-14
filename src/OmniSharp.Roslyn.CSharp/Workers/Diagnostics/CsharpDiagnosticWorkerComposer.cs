using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    // Theres several implementation of worker currently based on configuration.
    // This will handle switching between them.
    [Export(typeof(ICsDiagnosticWorker)), Shared]
    public class CsharpDiagnosticWorkerComposer: ICsDiagnosticWorker, IDisposable
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _providers;
        private readonly ILoggerFactory _loggerFactory;
        private readonly DiagnosticEventForwarder _forwarder;
        private ICsDiagnosticWorker _implementation;
        private readonly IDisposable _onChange;

        [ImportingConstructor]
        public CsharpDiagnosticWorkerComposer(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            DiagnosticEventForwarder forwarder,
            IOptionsMonitor<OmniSharpOptions> options)
        {
            _workspace = workspace;
            _providers = providers;
            _loggerFactory = loggerFactory;
            _forwarder = forwarder;
            _onChange = options.OnChange(UpdateImplementation);
            UpdateImplementation(options.CurrentValue);
        }

        private void UpdateImplementation(OmniSharpOptions options)
        {
            var firstRun = _implementation is null;
            if (options.RoslynExtensionsOptions.EnableAnalyzersSupport && (firstRun || _implementation is CSharpDiagnosticWorker))
            {
                var old = Interlocked.Exchange(ref _implementation, new CSharpDiagnosticWorkerWithAnalyzers(_workspace, _providers, _loggerFactory, _forwarder, options));
                if (old is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            else if (!options.RoslynExtensionsOptions.EnableAnalyzersSupport && (firstRun || _implementation is CSharpDiagnosticWorkerWithAnalyzers))
            {
                var old = Interlocked.Exchange(ref _implementation, new CSharpDiagnosticWorker(_workspace, _forwarder, _loggerFactory));
                if (old is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                if (!firstRun)
                {
                    _implementation.QueueDocumentsForDiagnostics();
                }
            }
        }

        public Task<ImmutableArray<DocumentDiagnostics>> GetAllDiagnosticsAsync()
        {
            return _implementation.GetAllDiagnosticsAsync();
        }

        public Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<string> documentPaths)
        {
            return _implementation.GetDiagnostics(documentPaths);
        }

        public ImmutableArray<DocumentId> QueueDocumentsForDiagnostics()
        {
            return _implementation.QueueDocumentsForDiagnostics();
        }

        public ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(ImmutableArray<ProjectId> projectIds)
        {
            return _implementation.QueueDocumentsForDiagnostics(projectIds);
        }

        public void Dispose()
        {
            if (_implementation is IDisposable disposable) disposable.Dispose();
            _onChange.Dispose();
        }

        public Task<IEnumerable<Diagnostic>> AnalyzeDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            return _implementation.AnalyzeDocumentAsync(document, cancellationToken);
        }

        public Task<IEnumerable<Diagnostic>> AnalyzeProjectsAsync(Project project, CancellationToken cancellationToken)
        {
            return _implementation.AnalyzeProjectsAsync(project, cancellationToken);
        }
    }
}
