using System.Composition;
using OmniSharp.Mef;
using OmniSharp.Models.Metadata;

namespace OmniSharp.Cake.Services.RequestHandlers.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.Metadata, Constants.LanguageNames.Cake), Shared]
    public class MetadataHandler : CakeRequestHandler<MetadataRequest, MetadataResponse>
    {
        [ImportingConstructor]
        public MetadataHandler(OmniSharpWorkspace workspace)
            : base(workspace)
        {
        }
    }
}
