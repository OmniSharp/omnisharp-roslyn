using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.DebugTestGetStartInfo, typeof(DebugTestGetStartInfoRequest), typeof(DebugTestGetStartInfoResponse))]
    public class DebugTestGetStartInfoRequest : SingleTestRequest
    {
    }
}
