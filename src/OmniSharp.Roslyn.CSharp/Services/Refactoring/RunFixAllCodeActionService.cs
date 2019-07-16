using System;
using System.Collections.Generic;
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
    [OmniSharpHandler(OmniSharpEndpoints.RunFixAll, LanguageNames.CSharp)]
    public class RunFixAllCodeActionService : FixAllCodeActionBase, IRequestHandler<RunFixAllRequest, RunFixAllResponse>
    {
        [ImportingConstructor]
        public RunFixAllCodeActionService(ICsDiagnosticWorker diagnosticWorker, CachingCodeFixProviderForProjects codeFixProvider, OmniSharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers) : base(diagnosticWorker, codeFixProvider, workspace, providers)
        {
        }

        public async Task<RunFixAllResponse> FixAll()
        {
            var solutionBeforeChanges = Workspace.CurrentSolution;
            var projects = Workspace.CurrentSolution.Projects;

            foreach (var project in projects)
            {
                foreach (var codeFixWithDiagnostics in await GetAvailableCodeFixes(project.Id))
                {
                    try
                    {
                        var fixAllContext = new FixAllContext(
                            project,
                            codeFixWithDiagnostics.CodeFixProvider,
                            FixAllScope.Project,
                            codeFixWithDiagnostics.CodeFixProvider.FixableDiagnosticIds.First(),
                            codeFixWithDiagnostics.MatchingDiagnostics.Select(x => x.Id),
                            codeFixWithDiagnostics,
                            CancellationToken.None
                        );

                        var fixes = await codeFixWithDiagnostics.FixAllProvider.GetFixAsync(fixAllContext);

                        if (fixes == default)
                            continue;

                        var operations = await fixes.GetOperationsAsync(CancellationToken.None);

                        foreach (var o in operations)
                        {
                            if (o is ApplyChangesOperation applyChangesOperation)
                            {
                                o.Apply(Workspace, CancellationToken.None);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }

            var currentSolution = Workspace.CurrentSolution;

            var getChangedDocumentIds = solutionBeforeChanges.GetChanges(currentSolution).GetProjectChanges().SelectMany(x => x.GetChangedDocuments());

            var changes = await Task.WhenAll(getChangedDocumentIds
                .Select(async x => (changes: await TextChanges.GetAsync(currentSolution.GetDocument(x), solutionBeforeChanges.GetDocument(x)), document: currentSolution.GetDocument(x))));

            return new RunFixAllResponse
            {
                Changes = changes.Select(x => new ModifiedFileResponse(x.document.FilePath) { Changes = x.changes.ToList() }).ToList()
            };
        }

        public async Task<RunFixAllResponse> Handle(RunFixAllRequest request)
        {
            return await FixAll();
        }
    }

    [OmniSharpEndpoint(OmniSharpEndpoints.RunFixAll, typeof(RunFixAllRequest), typeof(RunFixAllResponse))]
    public class RunFixAllRequest : SimpleFileRequest
    {
    }

    public class RunFixAllResponse : IAggregateResponse
    {
        public RunFixAllResponse()
        {
            Changes = new List<ModifiedFileResponse>();
        }

        public IEnumerable<ModifiedFileResponse> Changes { get; set; }

        public IAggregateResponse Merge(IAggregateResponse response) { return response; }
    }
}
