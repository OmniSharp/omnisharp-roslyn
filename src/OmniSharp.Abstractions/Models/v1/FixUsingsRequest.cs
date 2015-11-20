using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.FixUsings, typeof(FixUsingsRequest), typeof(FixUsingsResponse))]
    public class FixUsingsRequest : Request
    {
        public bool WantsTextChanges { get; set; }
    }
}
