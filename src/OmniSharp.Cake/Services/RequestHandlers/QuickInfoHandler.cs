using System.Composition;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Cake.Services.RequestHandlers
{
    [Shared]
    [OmniSharpHandler(OmniSharpEndpoints.QuickInfo, Constants.LanguageNames.Cake)]
    public class QuickInfoHandler : CakeRequestHandler<QuickInfoRequest, QuickInfoResponse>
    {
        [ImportingConstructor]
        public QuickInfoHandler(OmniSharpWorkspace workspace) : base(workspace)
        {
        }
    }
}
