using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/metadata", typeof(MetadataRequest), typeof(MetadataResponse))]
    public class MetadataRequest : MetadataSource, IRequest
    {
        public int Timeout { get; set; } = 2000;
    }
}
