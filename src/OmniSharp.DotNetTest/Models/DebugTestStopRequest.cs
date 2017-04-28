using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.DebugTestStop, typeof(DebugTestStopRequest), typeof(DebugTestStopResponse))]
    public class DebugTestStopRequest : Request
    {
    }
}
