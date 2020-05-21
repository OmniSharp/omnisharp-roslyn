using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.DebugTestsInClassGetStartInfo, typeof(DebugTestClassGetStartInfoRequest), typeof(DebugTestGetStartInfoResponse))]
    public class DebugTestClassGetStartInfoRequest :  MultiTestRequest
    {
    }
}
