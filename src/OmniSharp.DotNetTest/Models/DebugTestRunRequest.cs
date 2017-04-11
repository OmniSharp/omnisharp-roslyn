using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.V2.DebugTestRun, typeof(DebugTestRunRequest), typeof(DebugTestRunResponse))]
    public class DebugTestRunRequest : Request
    {
    }
}
