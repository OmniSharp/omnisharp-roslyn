using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models.FileClose;

namespace OmniSharp.Roslyn.CSharp.Services.Files
{
    [OmniSharpHandler(OmniSharpEndpoints.Close, LanguageNames.CSharp)]
    public class FileCloseService : IRequestHandler<FileCloseRequest, FileCloseResponse>
    {
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public FileCloseService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public Task<FileCloseResponse> Handle(FileCloseRequest request)
        {
            var documents = _workspace.GetDocuments(request.FileName);
            foreach (var document in documents)
            {
                _workspace.CloseDocument(document.Id);
            }
            return Task.FromResult(new FileCloseResponse());
        }
    }
}
