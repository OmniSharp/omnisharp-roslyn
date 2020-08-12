using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Abstractions.Models.V1.FixAll;
using OmniSharp.Mef;
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
            var availableFixes = await GetDiagnosticsMappedWithFixAllProviders(request.Scope, request.FileName);

            var distinctDiagnosticsThatCanBeFixed = availableFixes
                .SelectMany(x => x.FixableDiagnostics)
                .GroupBy(x => x.id) // Distinct isn't good fit here since theres cases where Id has multiple different messages based on location, just show one of them.
                .Select(x => x.First())
                .Select(x => new FixAllItem(x.id, x.messsage))
                .OrderBy(x => x.Id)
                .ToArray();

            return new GetFixAllResponse(distinctDiagnosticsThatCanBeFixed);
        }
    }
}