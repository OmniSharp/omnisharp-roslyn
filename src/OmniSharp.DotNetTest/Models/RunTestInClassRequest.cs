using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.RunAllTestsInClass, typeof(RunTestsInClassRequest), typeof(RunTestResponse))]
    public class RunTestsInClassRequest : MultiTestRequest
    {
    }
}
