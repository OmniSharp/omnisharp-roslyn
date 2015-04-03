using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("fixusings")]
        public async Task<FixUsingsResponse> FixUsings(Request request)
        {
            var response = new FixUsingsResponse(string.Empty, new List<QuickFix>());
            var document = _workspace.GetDocument(request.FileName);

            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                response = await FixUsingsWorker.AddMissingUsings(request.FileName, document, semanticModel);
            }

            return response;
        }
    }
}
