using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/metadata", typeof(MetadataRequest), typeof(MetadataResponse), TakeOne = true)]
    public class MetadataRequest : MetadataSource, IRequest
    {
        public int Timeout { get; set; } = 2000;
    }
}
