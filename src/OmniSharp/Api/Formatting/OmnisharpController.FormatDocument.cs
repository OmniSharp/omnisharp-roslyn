using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("codeformat")]
        public async Task<IActionResult> CodeFormat([FromBody]Request request)
        {
            _workspace.EnsureBufferUpdated(request);
            
            var options = _workspace.Options
                .WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, _options.FormattingOptions.NewLine)
                .WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, _options.FormattingOptions.UseTabs)
                .WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, _options.FormattingOptions.TabSize);
            
            var response = new CodeFormatResponse();

            var documentId = _workspace.GetDocumentId(request.FileName);
            if (documentId != null)
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                document = await Formatter.FormatAsync(document, options);
                response.Buffer = (await document.GetTextAsync()).ToString();
            }
            else
            {
                return new HttpNotFoundResult();
            }

            return new ObjectResult(response);
        }
    }
}