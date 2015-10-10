using System;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Buffer
{
    [OmniSharpHandler(OmnisharpEndpoints.UpdateBuffer, LanguageNames.CSharp)]
    public class UpdateBufferService : RequestHandler<UpdateBufferRequest, object>
    {
        private readonly BufferManager _bufferManager;

        [ImportingConstructor]
        public UpdateBufferService(BufferManager bufferManager)
        {
            _bufferManager = bufferManager;
        }

        public async Task<object> Handle(UpdateBufferRequest request)
        {
            await _bufferManager.UpdateBuffer(request);
            return true;
        }
    }
}
