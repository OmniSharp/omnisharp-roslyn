#nullable enable

using OmniSharp.Mef;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.DebugTestsInContextGetStartInfo, typeof(DebugTestsInContextGetStartInfoRequest), typeof(DebugTestGetStartInfoResponse))]
    public class DebugTestsInContextGetStartInfoRequest : BaseTestsInContextRequest
    {
    }
}
