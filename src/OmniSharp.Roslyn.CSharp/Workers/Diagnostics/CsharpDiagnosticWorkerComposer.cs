using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.Extensions.Logging;
using OmniSharp.Helpers;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    // Theres several implementation of worker currently based on configuration.
    // This will handle switching between them.
    [Export(typeof(ICsDiagnosticWorker)), Shared]
    public class CsharpDiagnosticWorkerComposer: ICsDiagnosticWorker
    {
        private readonly ICsDiagnosticWorker _implementation;
        private readonly OmniSharpWorkspace _workspace;

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
                _implementation = new CSharpDiagnosticWorkerWithAnalyzers(workspace, providers, loggerFactory, forwarder);
            }
            else
            {
                _implementation = new CSharpDiagnosticWorker(workspace, forwarder, loggerFactory);
            }

            _workspace = workspace;
        }

        public Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetAllDiagnosticsAsync()
        {
            return _implementation.GetAllDiagnosticsAsync();
        }

        public Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetDiagnostics(ImmutableArray<string> documentPaths)
        {
            return _implementation.GetDiagnostics(documentPaths);
        }

        public ImmutableArray<DocumentId> QueueAllDocumentsForDiagnostics()
        {
            return _implementation.QueueAllDocumentsForDiagnostics();
        }

        public ImmutableArray<DocumentId> QueueForDiagnosis(ImmutableArray<string> documentPaths)
        {
            return _implementation.QueueForDiagnosis(documentPaths);
        }
    }
}