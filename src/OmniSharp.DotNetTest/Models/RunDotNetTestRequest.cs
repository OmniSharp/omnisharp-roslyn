using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.RunDotNetTest, typeof(RunDotNetTestRequest), typeof(RunDotNetTestResponse))]
    public class RunDotNetTestRequest : Request
    {
        public string MethodName { get; set; }
    }
}