using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.V2.DebugDotNetTestReady, typeof(DebugDotNetTestReadyRequest), typeof(DebugDotNetTestReadyResponse))]
    public class DebugDotNetTestReadyRequest : Request
    {
    }
}
