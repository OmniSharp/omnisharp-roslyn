using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models.FileOpen;

namespace OmniSharp.Roslyn.CSharp.Services.Files
{
    [OmniSharpHandler(OmniSharpEndpoints.Open, LanguageNames.CSharp)]
    public class FileOpenService : IRequestHandler<FileOpenRequest, FileOpenResponse>
    {
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public FileOpenService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public Task<FileOpenResponse> Handle(FileOpenRequest request)
        {
            var documents = _workspace.GetDocuments(request.FileName);
            foreach (var document in documents)
            {
                _workspace.OpenDocument(document.Id, false);
            }
            return Task.FromResult(new FileOpenResponse());
        }
    }
}
