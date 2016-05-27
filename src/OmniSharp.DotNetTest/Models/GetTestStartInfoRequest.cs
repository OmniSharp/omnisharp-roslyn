using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.GetTestStartInfo, typeof(GetTestStartInfoRequest), typeof(GetTestStartInfoResponse))]
    public class GetTestStartInfoRequest : Request
    {
        public string MethodName { get; set; }
    }
}