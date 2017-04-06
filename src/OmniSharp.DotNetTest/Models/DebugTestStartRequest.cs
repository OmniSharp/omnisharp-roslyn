using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.V2.DebugTestStart, typeof(DebugTestStartRequest), typeof(DebugTestStartResponse))]
    public class DebugTestStartRequest : Request
    {
        public string MethodName { get; set; }
        public string TestFrameworkName { get; set; }
    }
}
