using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("updatebuffer")]
        public ObjectResult UpdateBuffer(Request request)
        {
            return new ObjectResult(true);
        }

        [HttpPost("changebuffer")]
        public async Task<ObjectResult> ChangeBuffer(ChangeBufferRequest request)
        {
            foreach (var documentId in _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName))
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                var sourceText = await document.GetTextAsync();
                var startOffset = sourceText.Lines.GetPosition(new LinePosition(request.StartLine - 1, request.StartColumn - 1));
                var endOffset = sourceText.Lines.GetPosition(new LinePosition(request.EndLine - 1, request.EndColumn - 1));

                sourceText = sourceText.WithChanges(new[] {
                    new Microsoft.CodeAnalysis.Text.TextChange(new TextSpan(startOffset, endOffset - startOffset), request.NewText)
                });

                _workspace.OnDocumentChanged(documentId, sourceText);
            }
            return new ObjectResult(true);
        }
    }
}