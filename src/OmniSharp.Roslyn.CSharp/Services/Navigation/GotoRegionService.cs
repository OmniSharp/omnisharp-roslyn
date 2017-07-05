using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.GotoRegion;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.GotoRegion, LanguageNames.CSharp)]
    public class GotoRegionService : IRequestHandler<GotoRegionRequest, QuickFixResponse>
    {
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public GotoRegionService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<QuickFixResponse> Handle(GotoRegionRequest request)
        {
            var regions = new List<QuickFix>();
            var document = _workspace.GetDocument(request.FileName);

            if (document != null)
            {
                var root = await document.GetSyntaxRootAsync();
                var regionTrivias = root.DescendantNodesAndTokens()
                    .Where(node => node.HasLeadingTrivia)
                    .SelectMany(node => node.GetLeadingTrivia())
                    .Where(x => (x.RawKind == (int)SyntaxKind.RegionDirectiveTrivia ||
                                  x.RawKind == (int)SyntaxKind.EndRegionDirectiveTrivia));

                foreach (var regionTrivia in regionTrivias.Distinct())
                {
                    regions.Add(regionTrivia.GetLocation().GetQuickFix(_workspace));
                }
            }
            return new QuickFixResponse(regions);
        }
    }
}
