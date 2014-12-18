using System.Collections.Generic;
using System.Linq;
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
        private List<QuickFix> _quickFixes = new List<QuickFix>();
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
                

                foreach (var usage in usages)
                {
                    foreach (var location in usage.Locations)
                    {
                        AddQuickFix(location.Location);
                    }

                    foreach (var location in usage.Definition.Locations)
                    {
                        AddQuickFix(location);
                    }
                }

                if (_quickFixes.Any())
                {
                    response = new QuickFixResponse(_quickFixes.OrderBy(q => q.FileName).ThenBy(q => q.Line));
                }
            }

            return new ObjectResult(response);
        }

        private async void AddQuickFix(Location location)
        {
            if (location.IsInSource)
            {
                var lineSpan = location.GetLineSpan();
                var path = lineSpan.Path;
                var document = _workspace.GetDocument(path);
                var line = lineSpan.StartLinePosition.Line;
                var syntaxTree = await document.GetSyntaxTreeAsync();
                var text = syntaxTree.GetText().Lines[line].ToString();

                _quickFixes.Add(new QuickFix
                {
                    Text = text.Trim(),
                    FileName = path,
                    Line = line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1
                });
            }
        }
    }
}
