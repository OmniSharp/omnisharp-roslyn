using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.GetTestStartInfo, typeof(GetTestStartInfoRequest), typeof(GetTestStartInfoResponse))]
    public class GetTestStartInfoRequest : SingleTestRequest
    {
    }
}
