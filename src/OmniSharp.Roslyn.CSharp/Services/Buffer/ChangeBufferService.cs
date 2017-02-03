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
        private OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public ChangeBufferService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<object> Handle(ChangeBufferRequest request)
        {
            await _workspace.BufferManager.UpdateBuffer(request);
            return true;
        }
    }
}
