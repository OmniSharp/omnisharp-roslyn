using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    // Theres several implementation of worker currently based on configuration.
    // This will handle switching between them.
    [Export(typeof(ICsDiagnosticWorker)), Shared]
    public class CsharpDiagnosticWorkerComposer: ICsDiagnosticWorker
    {
        private readonly ICsDiagnosticWorker _implementation;

        [ImportingConstructor]
        public CsharpDiagnosticWorkerComposer(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            DiagnosticEventForwarder forwarder,
            OmniSharpOptions options)
        {
            if(options.RoslynExtensionsOptions.EnableAnalyzersSupport)
            {
                _implementation = new CSharpDiagnosticWorkerWithAnalyzers(workspace, providers, loggerFactory, forwarder, options);
            }
            else
            {
                _implementation = new CSharpDiagnosticWorker(workspace, forwarder, loggerFactory);
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
    }
}