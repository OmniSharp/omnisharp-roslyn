using Microsoft.AspNet.Mvc;

namespace OmniSharp
{
    [Route("/")]
    public partial class OmnisharpController
    {
        private readonly OmnisharpWorkspace _workspace;

        public OmnisharpController(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }
    }
}