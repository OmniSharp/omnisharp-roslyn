using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
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

            var documentId = _workspace.GetDocumentId(request.FileName);
            var response = new QuickFixResponse();
            if (documentId != null)
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);
                var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, _workspace.CurrentSolution);
                var usages = await SymbolFinder.FindReferencesAsync(definition, _workspace.CurrentSolution);
                var quickFixes = new List<QuickFix>();

                foreach (var usage in usages)
                {
                    foreach (var location in usage.Locations)
                    {
                        quickFixes.Add(GetQuickFix(location.Location));
                    }

                    foreach (var location in usage.Definition.Locations)
                    {
                        quickFixes.Add(GetQuickFix(location));
                    }
                }

                response = new QuickFixResponse(quickFixes);
            }

            return new ObjectResult(response);
        }

        private QuickFix GetQuickFix(Location location)
        {
            var path = location.GetLineSpan().Path;
            var document = _workspace.GetDocument(path);
            var lineSpan = location.GetLineSpan();
            var line = lineSpan.StartLinePosition.Line;
            var text = document.GetSyntaxTreeAsync().Result.GetText().Lines[line].ToString();
            
            return new QuickFix
            {
                Text = text.Trim(),
                FileName = path,
                Line = line + 1,
                Column = lineSpan.StartLinePosition.Character + 1
            };
        }
    }
}