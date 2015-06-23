using Microsoft.AspNet.Mvc;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("checkalivestatus")]
        public bool CheckAliveStatus()
        {
            return true;
        }

        [HttpPost("checkreadystatus")]
        public bool CheckReadyStatus()
        {
            return _workspace.Initialized;
        }
    }
}