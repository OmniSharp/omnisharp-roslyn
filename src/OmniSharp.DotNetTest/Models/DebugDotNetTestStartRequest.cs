using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.V2.DebugDotNetTestStart, typeof(DebugDotNetTestStartRequest), typeof(DebugDotNetTestStartResponse))]
    public class DebugDotNetTestStartRequest : Request
    {
        public string MethodName { get; set; }
        public string TestFrameworkName { get; set; }
    }
}
