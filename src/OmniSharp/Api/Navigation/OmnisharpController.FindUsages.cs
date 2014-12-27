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
        [HttpPost("findusages")]
        public async Task<IActionResult> FindUsages([FromBody]Request request)
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
                var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, _workspace.CurrentSolution);
                var usages = await SymbolFinder.FindReferencesAsync(definition ?? symbol, _workspace.CurrentSolution);

                var quickFixes = new List<QuickFix>();

                foreach (var usage in usages)
                {
                    foreach (var location in usage.Locations)
                    {
                        AddQuickFix(quickFixes, location.Location);
                    }

                    foreach (var location in usage.Definition.Locations)
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
