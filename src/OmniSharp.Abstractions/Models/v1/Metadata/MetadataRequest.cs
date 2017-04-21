using OmniSharp.Mef;

namespace OmniSharp.Models.Metadata
{
    [OmniSharpEndpoint(OmniSharpEndpoints.Metadata, typeof(MetadataRequest), typeof(MetadataResponse))]
    public class MetadataRequest : MetadataSource, IRequest
    {
        public int Timeout { get; set; } = 2000;
    }
}
