using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.FixAll, LanguageNames.CSharp)]
    public class FixAllCodeActionService : IRequestHandler<FixAllRequest, FixAllResponse>
    {
        private readonly ICsDiagnosticWorker _diagnosticWorker;
        private readonly CachingCodeFixProviderForProjects _codeFixProvider;
        private readonly OmniSharpWorkspace _workspace;
        private readonly IEnumerable<ICodeActionProvider> _providers;

        [ImportingConstructor]
        public FixAllCodeActionService(ICsDiagnosticWorker diagnosticWorker, CachingCodeFixProviderForProjects codeFixProvider, OmniSharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers)
        {
            _diagnosticWorker = diagnosticWorker;
            _codeFixProvider = codeFixProvider;
            _workspace = workspace;
            _providers = providers;
        }

        public async Task<FixAllResponse> FixAll()
        {
            // https://csharp.hotexamples.com/examples/-/FixAllContext/-/php-fixallcontext-class-examples.html

            var diagnostics = await _diagnosticWorker.GetAllDiagnosticsAsync();
            var solutionBeforeChanges = _workspace.CurrentSolution;

            var projects = _workspace.CurrentSolution.Projects;

            foreach (var project in projects)
            {
                var allCodefixesForProject =
                    _providers.SelectMany(provider => provider.CodeFixProviders)
                        .Concat(_codeFixProvider.GetAllCodeFixesForProject(project.Id))
                        .Where(x => x.GetFixAllProvider() != null);

                var codeFixesWithMatchingDiagnostics = allCodefixesForProject
                    .Select(codeFix => (codeFix, diagnostics: diagnostics.Where(x => HasFix(codeFix, x.diagnostic.Id))))
                    .Where(x => x.diagnostics.Any());

                foreach (var codeFixWithDiagnostics in codeFixesWithMatchingDiagnostics)
                {
                    try
                    {
                        // TODO: Provider should only return diagnostics from correct context.
                        var fixAllContext = new FixAllContext(
                        project,
                        codeFixWithDiagnostics.codeFix,
                        FixAllScope.Project,
                        codeFixWithDiagnostics.codeFix.FixableDiagnosticIds.First(),
                        codeFixWithDiagnostics.diagnostics.Select(x => x.diagnostic.Id),
                        new FixAllDiagnosticProvider(codeFixWithDiagnostics.diagnostics.Select(x => x.diagnostic).ToImmutableArray()),
                        CancellationToken.None);

                        var provider = codeFixWithDiagnostics.codeFix.GetFixAllProvider();

                        var fixes = await provider.GetFixAsync(fixAllContext);

                        if (fixes == default)
                            continue;


                        var operations = await fixes.GetOperationsAsync(CancellationToken.None);

                        foreach (var o in operations)
                        {
                            Console.WriteLine($"Operation {o.Title}");

                            if (o is ApplyChangesOperation applyChangesOperation)
                            {
                                Console.WriteLine($"Applying {o.Title}");
                                o.Apply(_workspace, CancellationToken.None);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }

            var currentSolution = _workspace.CurrentSolution;

            var changesX = solutionBeforeChanges.GetChanges(currentSolution).GetProjectChanges().ToList();

            var getChangedDocumentIds = solutionBeforeChanges.GetChanges(currentSolution).GetProjectChanges().SelectMany(x => x.GetChangedDocuments());

            var changes = await Task.WhenAll(getChangedDocumentIds
                .Select(async x => (changes: await TextChanges.GetAsync(currentSolution.GetDocument(x), solutionBeforeChanges.GetDocument(x)), document: currentSolution.GetDocument(x))));

            var foo = new FixAllResponse
            {
                Changes = changes.Select(x => new ModifiedFileResponse(x.document.FilePath) { Changes = x.changes.ToList() }).ToList()
            };

            return foo;
        }

        public async Task<FixAllResponse> Handle(FixAllRequest request)
        {
            await FixAll();
            return new FixAllResponse();
        }

        private bool HasFix(CodeFixProvider codeFixProvider, string diagnosticId)
        {
            return codeFixProvider.FixableDiagnosticIds.Any(id => id == diagnosticId);
        }

        private class FixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
        {
            private readonly ImmutableArray<Diagnostic> _diagnostics;

            public FixAllDiagnosticProvider(ImmutableArray<Diagnostic> diagnostics)
            {
                _diagnostics = diagnostics;
            }

            public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                return Task.FromResult<IEnumerable<Diagnostic>>(_diagnostics);
            }

            public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
                => Task.FromResult(_diagnostics.Where(x => string.Compare(x.Location.GetMappedLineSpan().Path, document.FilePath, true) == 0));

            public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
                => Task.FromResult<IEnumerable<Diagnostic>>(_diagnostics);
        }
    }

    [OmniSharpEndpoint(OmniSharpEndpoints.FixAll, typeof(FixAllRequest), typeof(FixAllResponse))]
    public class FixAllRequest : SimpleFileRequest
    {
    }

    public class FixAllResponse : IAggregateResponse
    {
        public FixAllResponse()
        {
            Changes = new List<ModifiedFileResponse>();
        }

        public IEnumerable<ModifiedFileResponse> Changes { get; set; }
        public IEnumerable<string> WhatIsThisShit { get; set; } = new List<string>() { "SomethingHere" };

        public IAggregateResponse Merge(IAggregateResponse response) { return response; }
    }
}
