using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    
    public partial class OmnisharpController
    {
        [HttpPost("findimplementations")]
        public async Task<IActionResult> FindImplementations([FromBody]Request request)
        {
            _workspace.EnsureBufferUpdated(request);

            var document = _workspace.GetDocument(request.FileName);
            var response = new QuickFixResponse();
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);
                var implementations = await SymbolFinder.FindImplementationsAsync(symbol, _workspace.CurrentSolution);

                var quickFixes = new List<QuickFix>();

                foreach (var implementation in implementations)
                {
                    foreach (var location in implementation.Locations)
                    {
                        AddQuickFix(quickFixes, location);
                    }
                }

                response = new QuickFixResponse(quickFixes.OrderBy(q => q.FileName)
                                                            .ThenBy(q => q.Line)
                                                            .ThenBy(q => q.Column));
            }
            
            return new ObjectResult(response);
        }
    }
}
