using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;

namespace OmniSharp
{
#if DNX451
    public partial class OmnisharpController
    {
        [HttpPost("fixusings")]
        public async Task<FixUsingsResponse> FixUsings(FixUsingsRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var docText = await document.GetTextAsync();
            var response = new FixUsingsResponse();

            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                response = await FixUsingsWorker.FixUsings(_workspace, request.FileName, document, semanticModel, request.WantsTextChanges);
            }

            return response;
        }
    }
#endif
}
