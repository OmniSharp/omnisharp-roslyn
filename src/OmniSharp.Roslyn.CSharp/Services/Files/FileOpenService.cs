using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Files
{
    [OmniSharpHandler(OmnisharpEndpoints.Open, LanguageNames.CSharp)]
    public class FileOpenService : RequestHandler<FileOpenRequest, FileOpenResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public FileOpenService(OmnisharpWorkspace workspace)
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
