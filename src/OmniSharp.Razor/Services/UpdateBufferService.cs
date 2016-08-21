using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Razor.Services
{
    [OmniSharpHandler(OmnisharpEndpoints.UpdateBuffer, RazorLanguage.Razor)]
    public class UpdateBufferService : RequestHandler<UpdateBufferRequest, object>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public UpdateBufferService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<object> Handle(UpdateBufferRequest request)
        {
            await _workspace.BufferManager.UpdateBuffer(request);
            return true;
        }
    }
}
