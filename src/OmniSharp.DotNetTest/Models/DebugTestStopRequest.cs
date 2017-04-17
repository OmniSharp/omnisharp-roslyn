using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.V2.DebugTestStop, typeof(DebugTestStopRequest), typeof(DebugTestStopResponse))]
    public class DebugTestStopRequest : Request
    {
    }
}
