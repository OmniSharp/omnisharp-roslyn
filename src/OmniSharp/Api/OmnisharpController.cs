using Microsoft.AspNet.Mvc;
using Microsoft.Framework.OptionsModel;
using OmniSharp.Options;

namespace OmniSharp
{
    [Route("/")]
    public partial class OmnisharpController
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly OmniSharpOptions _options;

        public OmnisharpController(OmnisharpWorkspace workspace, IOptions<OmniSharpOptions> optionsAccessor)
        {
            _workspace = workspace;
            _options = optionsAccessor != null ? optionsAccessor.Options : new OmniSharpOptions();
        }
    }
}