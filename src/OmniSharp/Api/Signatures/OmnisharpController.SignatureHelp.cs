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

        [HttpPost("signatureHelp")]
        public async Task<object> SignatureLookup(Request request)
        {
            var symbol = await FindSymbol(request);

            return null;
        }

        private async Task<ISymbol> FindSymbol(Request request)
        {
            foreach (var document in _workspace.GetDocuments(request.FileName))
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);
                if (symbol != null)
                {
                    return symbol;
                }
            }

            return null;
        }
    }    
}