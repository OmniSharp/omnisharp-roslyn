using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{
    [OmniSharpHandler(OmniSharpEndpoints.GetFixAll, LanguageNames.CSharp)]
    public class GetFixAllCodeActionService : FixAllCodeActionBase, IRequestHandler<GetFixAllRequest, GetFixAllResponse>
    {
        [ImportingConstructor]
        public GetFixAllCodeActionService(ICsDiagnosticWorker diagnosticWorker, CachingCodeFixProviderForProjects codeFixProvider, OmniSharpWorkspace workspace, [ImportMany] IEnumerable<ICodeActionProvider> providers) : base(diagnosticWorker, codeFixProvider, workspace, providers)
        {
        }

        public async Task<GetFixAllResponse> Handle(GetFixAllRequest request)
        {
            var allProjectIds = Workspace.CurrentSolution.Projects.Select(x => x.Id);

            var availableFixes = await Task.WhenAll(allProjectIds.Select(id => GetAvailableCodeFixes(id)));

            var distinctDiagnosticsThatCanBeFixed = availableFixes
                .SelectMany(x => x)
                .SelectMany(x => x.MatchingDiagnostics)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .Select(x => new FixAllItem(x.Id, x.GetMessage()))
                .ToArray();

            return new GetFixAllResponse(distinctDiagnosticsThatCanBeFixed);
        }
    }

    public class GetFixAllResponse
    {
        public GetFixAllResponse(IEnumerable<FixAllItem> fixableItems)
        {
            Items = fixableItems;
        }

        public IEnumerable<FixAllItem> Items { get; set; }
    }

    public class FixAllItem
    {
        public FixAllItem(string id, string message)
        {
            Id = id;
            Message = message;
        }

        public string Id { get; set; }
        public string Message { get; set; }
    }

    [OmniSharpEndpoint(OmniSharpEndpoints.GetFixAll, typeof(GetFixAllRequest), typeof(GetFixAllResponse))]
    public class GetFixAllRequest: SimpleFileRequest
    {
    }
}