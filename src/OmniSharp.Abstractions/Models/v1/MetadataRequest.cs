using Microsoft.CodeAnalysis;
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.Metadata, typeof(MetadataRequest), typeof(MetadataResponse))]
    public class MetadataRequest : MetadataSource, IRequest
    {
        public int Timeout { get; set; } = 2000;
    }
}
