using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models.UpdateBuffer;

namespace OmniSharp.Roslyn.CSharp.Services.Buffer
{
    [OmniSharpHandler(OmniSharpEndpoints.UpdateBuffer, LanguageNames.CSharp)]
    public class UpdateBufferService : IRequestHandler<UpdateBufferRequest, object>
    {
        private OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public UpdateBufferService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<object> Handle(UpdateBufferRequest request)
        {
            // To properly handle the request wait until all projects are loaded.
            await _workspace.BufferManager.UpdateBufferInFullProjectModelAsync(request);
            return true;
        }
    }
}
