using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.V2.DebugTestReady, typeof(DebugTestReadyRequest), typeof(DebugTestReadyResponse))]
    public class DebugTestReadyRequest : Request
    {
    }
}
