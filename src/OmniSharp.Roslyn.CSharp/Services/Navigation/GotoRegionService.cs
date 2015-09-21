using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(typeof(RequestHandler<GotoRegionRequest, QuickFixResponse>), LanguageNames.CSharp)]
    public class GotoRegionService : RequestHandler<GotoRegionRequest, QuickFixResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public GotoRegionService(OmnisharpWorkspace workspace)
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
                    regions.Add(await QuickFixHelper.GetQuickFix(_workspace, regionTrivia.GetLocation()));
                }
            }
            return new QuickFixResponse(regions);
        }
    }
}
