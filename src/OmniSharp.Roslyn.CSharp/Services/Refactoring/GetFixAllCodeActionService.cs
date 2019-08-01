using System;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Abstractions.Models.V1.FixAll;
using OmniSharp.Mef;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.GetFixAll, LanguageNames.CSharp)]
    public class GetFixAllCodeActionService : FixAllCodeActionBase, IRequestHandler<GetFixAllRequest, GetFixAllResponse>
    {
        [ImportingConstructor]
        public GetFixAllCodeActionService(ICsDiagnosticWorker diagnosticWorker, CachingCodeFixProviderForProjects codeFixProvider, OmniSharpWorkspace workspace) : base(diagnosticWorker, codeFixProvider, workspace)
        {
        }

        public async Task<GetFixAllResponse> Handle(GetFixAllRequest request)
        {
            var availableFixes = await GetDiagnosticsMappedWithFixAllProviders();

            var projectIdsInScope = GetProjectIdsInScope(request.Scope, request.FileName);

            var distinctDiagnosticsThatCanBeFixed = availableFixes
                .Where(x => IsFixOnScope(x, request.Scope, request.FileName))
                .SelectMany(x => x.GetAvailableFixableDiagnostics())
                .GroupBy(x => x.id)
                .Select(x => x.First())
                .Select(x => new FixAllItem(x.id, x.message))
                .ToArray();

            return new GetFixAllResponse(distinctDiagnosticsThatCanBeFixed);
        }
    }
}