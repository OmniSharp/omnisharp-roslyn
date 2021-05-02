#nullable enable

using OmniSharp.Mef;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.RunTestsInContext, typeof(RunTestsInContextRequest), typeof(RunTestResponse))]
    public class RunTestsInContextRequest : BaseTestsInContextRequest
    {
    }
}
