using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.FileSystem;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    [Export(typeof(ICsDiagnosticWorker)), Shared]
    public class CsharpDiagnosticWorkerComposer : CSharpDiagnosticWorkerBase, IDisposable
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _providers;
        private readonly ILoggerFactory _loggerFactory;
        private readonly DiagnosticEventForwarder _forwarder;
        private ICsDiagnosticWorker _implementation;
        private readonly IDisposable _onChange;
        private readonly FileSystemHelper _fileSystemHelper;

        [ImportingConstructor]
        public CsharpDiagnosticWorkerComposer(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            DiagnosticEventForwarder forwarder,
            IOptionsMonitor<OmniSharpOptions> options,
            FileSystemHelper fileSystemHelper)
            : base(workspace, fileSystemHelper)
        {
            _workspace = workspace;
            _providers = providers;
            _loggerFactory = loggerFactory;
            _forwarder = forwarder;
            _onChange = options.OnChange(UpdateImplementation);
            _fileSystemHelper = fileSystemHelper;
            UpdateImplementation(options.CurrentValue);
        }

        private void UpdateImplementation(OmniSharpOptions options)
        {
            bool firstRun = _implementation is null;
            bool? recreateWithAnalyzers = null;
            if (options.RoslynExtensionsOptions.EnableAnalyzersSupport && (firstRun || _implementation?.AnalyzersEnabled == false))
            {
                recreateWithAnalyzers = true;
            }
            else if (!options.RoslynExtensionsOptions.EnableAnalyzersSupport && (firstRun || _implementation?.AnalyzersEnabled == true))
            {
                recreateWithAnalyzers = false;
            }

            if (recreateWithAnalyzers is null)
            {
                return;
            }

            ICsDiagnosticWorker old = Interlocked.Exchange(
                    ref _implementation,
                    new CSharpDiagnosticWorkerWithAnalyzers(
                        _workspace, _providers, _loggerFactory, _forwarder, options, _fileSystemHelper, recreateWithAnalyzers!.Value));
            if (old is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public override bool AnalyzersEnabled => _implementation.AnalyzersEnabled;

        public override Task<ImmutableArray<DocumentDiagnostics>> GetAllDiagnosticsAsync()
        {
            return _implementation.GetAllDiagnosticsAsync();
        }

        public override Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<string> documentPaths)
        {
            return _implementation.GetDiagnostics(documentPaths);
        }

        public void Dispose()
        {
            if (_implementation is IDisposable disposable) disposable.Dispose();
            _onChange.Dispose();
        }

        public override Task<IEnumerable<Diagnostic>> AnalyzeDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            return _implementation.AnalyzeDocumentAsync(document, cancellationToken);
        }

        public override Task<IEnumerable<Diagnostic>> AnalyzeProjectsAsync(Project project, CancellationToken cancellationToken)
        {
            return _implementation.AnalyzeProjectsAsync(project, cancellationToken);
        }

        public override ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(IEnumerable<Document> documents, AnalyzerWorkType workType) =>
            _implementation.QueueDocumentsForDiagnostics(documents, workType);
    }
}
