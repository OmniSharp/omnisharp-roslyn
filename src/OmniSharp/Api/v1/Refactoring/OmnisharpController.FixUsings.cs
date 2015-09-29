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
            var response = new FixUsingsResponse();

            if (document != null)
            {
                response = await new FixUsingsWorker().FixUsings(_workspace, document);
                if (!request.WantsTextChanges)
                {
                    // return the new document
                    var docText = await _workspace.CurrentSolution.GetDocument(document.Id).GetTextAsync();
                    response.Buffer = docText.ToString();
                }
                else
                {
                    // return the text changes
                    var changes = await _workspace.CurrentSolution.GetDocument(document.Id).GetTextChangesAsync(document);
                    response.Changes = await LinePositionSpanTextChange.Convert(document, changes);
                }
            }

            return response;
        }
    }
#endif
}
