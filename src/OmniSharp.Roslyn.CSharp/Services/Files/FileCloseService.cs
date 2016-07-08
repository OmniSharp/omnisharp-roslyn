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
    [OmniSharpHandler(OmnisharpEndpoints.Close, LanguageNames.CSharp)]
    public class FileCloseService : RequestHandler<FileCloseRequest, FileCloseResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public FileCloseService(OmnisharpWorkspace workspace)
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
