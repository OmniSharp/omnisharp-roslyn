using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models.V2.CodeStructure;

namespace OmniSharp.Roslyn.CSharp.Services.Structure
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.CodeStructure, LanguageNames.CSharp)]
    public class CodeStructureService : IRequestHandler<CodeStructureRequest, CodeStructureResponse>
    {
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public CodeStructureService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public Task<CodeStructureResponse> Handle(CodeStructureRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return null;
            }

            var response = new CodeStructureResponse
            {
                Elements = Array.Empty<CodeElement>()
            };

            return Task.FromResult(response);
        }
    }
}
