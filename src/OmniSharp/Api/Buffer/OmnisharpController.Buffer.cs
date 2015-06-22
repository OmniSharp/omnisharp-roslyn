using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("updatebuffer")]
        public ObjectResult UpdateBuffer(Request request)
        {
            return new ObjectResult(true);
        }

        [HttpPost("changebuffer")]
        public async Task<ObjectResult> ChangeBuffer(ChangeBufferRequest request)
        {
            await _workspace.BufferManager.UpdateBuffer(request);
            return new ObjectResult(true);
        }
    }
}
