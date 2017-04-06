using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.V2.DebugTestCheck, typeof(DebugTestCheckRequest), typeof(DebugTestCheckResponse))]
    public class DebugTestCheckRequest : Request
    {
    }
}
