using System;
using Microsoft.AspNet.Mvc;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("checkreadystatus")]
        public bool CheckReadyStatus()
        {
            return _workspace.Initialized;
        }
    }
}