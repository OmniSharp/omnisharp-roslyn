using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
    
namespace OmniSharp
{   
    public partial class OmnisharpController
    {
        [HttpPost("gotoregion")]
        public async Task<QuickFixResponse> GoToRegion(Request request)
        {
            var regions = new List<QuickFix>();
            var document = _workspace.GetDocument(request.FileName);
            
            if (document != null)
            {
                var root = await document.GetSyntaxRootAsync();
                var regionTrivias = root.DescendantNodesAndTokens()
                    .Where(node => node.HasLeadingTrivia)
                    .SelectMany(node => node.GetLeadingTrivia())
                    .Where(x => (x.RawKind == (int) SyntaxKind.RegionDirectiveTrivia ||
                                  x.RawKind == (int) SyntaxKind.EndRegionDirectiveTrivia));
                                  
                foreach (var regionTrivia in regionTrivias.Distinct())
                {
                    regions.Add(await GetQuickFix(regionTrivia.GetLocation()));
                }
            }
            return new QuickFixResponse(regions);
        } 
    }
}