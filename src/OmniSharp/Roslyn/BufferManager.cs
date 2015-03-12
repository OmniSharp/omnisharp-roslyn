using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp.Roslyn
{
    public class BufferManager
    {

        private readonly OmnisharpWorkspace _workspace;

        public BufferManager(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public void UpdateBuffer(Request request)
        {
            if (request.Buffer == null || request.FileName == null)
            {
                return;
            }

            var sourceText = SourceText.From(request.Buffer);
            var documentIds = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName);
            foreach (var documentId in documentIds)
            {
                _workspace.OnDocumentChanged(documentId, sourceText);
            }
        }

        public async Task UpdateBuffer(ChangeBufferRequest request)
        {
            if (request.FileName == null)
            {
                return;
            }

            var documentIds = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName);
            foreach (var documentId in documentIds)
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                var sourceText = await document.GetTextAsync();
                var startOffset = sourceText.Lines.GetPosition(new LinePosition(request.StartLine - 1, request.StartColumn - 1));
                var endOffset = sourceText.Lines.GetPosition(new LinePosition(request.EndLine - 1, request.EndColumn - 1));

                sourceText = sourceText.WithChanges(new[] {
                    new TextChange(new TextSpan(startOffset, endOffset - startOffset), request.NewText)
                });

                _workspace.OnDocumentChanged(documentId, sourceText);
            }
        }
    }
}