using System.IO;
using System.Text;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    [Route("/")]
    public partial class OmnisharpController
    {
        private readonly OmnisharpWorkspace _workspace;

        public OmnisharpController(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }
        
        // Move this to a filter or something so that individual actions
        // don't have to do it on demand
        private void EnsureBufferUpdated(Request request)
        {
            foreach (var documentId in _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName))
            {
                var buffer = Encoding.UTF8.GetBytes(request.Buffer);
                var sourceText = SourceText.From(new MemoryStream(buffer), encoding: Encoding.UTF8);
                _workspace.OnDocumentChanged(documentId, sourceText);
            }
        }
    }
}