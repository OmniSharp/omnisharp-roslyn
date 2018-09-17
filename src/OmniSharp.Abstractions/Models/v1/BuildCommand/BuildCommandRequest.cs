using OmniSharp.Mef;

namespace OmniSharp.Models.BuildCommand
{
    [OmniSharpEndpoint(OmniSharpEndpoints.BuildCommand, typeof(BuildCommandRequest), typeof(QuickFixResponse))]
    public class BuildCommandRequest : Request
    {
        public BuildType Type { get; set; }
    }
}
