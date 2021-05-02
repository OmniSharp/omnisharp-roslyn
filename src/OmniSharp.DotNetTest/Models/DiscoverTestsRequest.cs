using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.DiscoverTests, typeof(DiscoverTestsRequest), typeof(DiscoverTestsResponse))]
    public class DiscoverTestsRequest : BaseTestRequest
    {
    }
}
