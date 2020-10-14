using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Abstractions.Models.V1.FixAll;
using OmniSharp.Mef;
using OmniSharp.Roslyn.CSharp.Helpers;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.GetFixAll, LanguageNames.CSharp)]
    public class GetFixAllCodeActionService : BaseCodeActionService<GetFixAllRequest, GetFixAllResponse>
    {
        [ImportingConstructor]
        public GetFixAllCodeActionService(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeActionProvider> providers,
            ILoggerFactory loggerFactory,
            ICsDiagnosticWorker diagnostics,
            CachingCodeFixProviderForProjects codeFixesForProject
        ) : base(workspace, providers, loggerFactory.CreateLogger<GetFixAllCodeActionService>(), diagnostics, codeFixesForProject)
        {
        }

        public override async Task<GetFixAllResponse> Handle(GetFixAllRequest request)
        {
            var document = Workspace.GetDocument(request.FileName);
            if (document is null)
            {
                Logger.LogWarning("Could not find document for file {0}", request.FileName);
                return new GetFixAllResponse(ImmutableArray<FixAllItem>.Empty);
            }

            var allDiagnostics = await GetDiagnosticsAsync(request.Scope, document);
            var validFixes = allDiagnostics
                .GroupBy(docAndDiag => docAndDiag.ProjectId)
                .SelectMany(grouping =>
                {
                    var projectFixProviders = GetCodeFixProviders(grouping.Key);
                    return grouping
                        .SelectMany(docAndDiag => docAndDiag.Diagnostics)
                        .Where(diag => projectFixProviders.Any(provider => provider.HasFixForId(diag.Id)));
                })
                .GroupBy(diag => diag.Id)
                .Select(grouping => grouping.First())
                .Select(x => new FixAllItem(x.Id, x.GetMessage()))
                .OrderBy(x => x.Id)
                .ToArray();

            return new GetFixAllResponse(validFixes);
        }
    }
}
