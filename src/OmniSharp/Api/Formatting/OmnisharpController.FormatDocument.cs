using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("codeformat")]
        public async Task<IActionResult> CodeFormat([FromBody]Request request)
        {
            _workspace.EnsureBufferUpdated(request);

            var response = new CodeFormatResponse();

            var documentId = _workspace.GetDocumentId(request.FileName);

            if (documentId != null)
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var model = await document.GetSemanticModelAsync();

                document = await Formatter.FormatAsync(document);

                if (_workspace.TryApplyChanges(document.Project.Solution))
                {
                    response.Buffer = (await document.GetTextAsync()).ToString();
                }
            }
            else
            {
                return new HttpNotFoundResult();
            }

            return new ObjectResult(response);
        }
    }
}