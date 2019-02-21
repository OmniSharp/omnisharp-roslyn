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

        [ImportingConstructor]
        public CsharpDiagnosticWorkerComposer(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            DiagnosticEventForwarder forwarder,
            RulesetsForProjects rulesetsForProjects,
            OmniSharpOptions options)
        {
            if(options.RoslynExtensionsOptions.EnableAnalyzersSupport)
            {
                _implementation = new CSharpDiagnosticWorkerWithAnalyzers(workspace, providers, loggerFactory, forwarder, rulesetsForProjects);
            }
            else
            {
                _implementation = new CSharpDiagnosticWorker(workspace, forwarder, loggerFactory);
            }
        }

        public Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetDiagnostics(ImmutableArray<Document> documents)
        {
            return _implementation.GetDiagnostics(documents);
        }

        public void QueueForDiagnosis(ImmutableArray<Document> documents)
        {
            _implementation.QueueForDiagnosis(documents);
        }
    }
}