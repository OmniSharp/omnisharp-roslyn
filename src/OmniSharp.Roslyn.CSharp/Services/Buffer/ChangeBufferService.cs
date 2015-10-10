using System.Composition;
﻿using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Buffer
{
    [OmniSharpHandler(OmnisharpEndpoints.ChangeBuffer, LanguageNames.CSharp)]
    public class ChangeBufferService : RequestHandler<ChangeBufferRequest, object>
    {
        private readonly BufferManager _bufferManager;

        [ImportingConstructor]
        public ChangeBufferService(BufferManager bufferManager)
        {
            _bufferManager = bufferManager;
        }

        public async Task<object> Handle(ChangeBufferRequest request)
        {
            await _bufferManager.UpdateBuffer(request);
            return true;
        }
    }
}
