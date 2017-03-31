using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.V2.GetDotNetTestStartInfo, typeof(GetDotNetTestStartInfoRequest), typeof(GetDotNetTestStartInfoResponse))]
    public class GetDotNetTestStartInfoRequest : Request
    {
        public string MethodName { get; set; }

        public string TestFrameworkName { get; set; }
    }
}
