using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Abstractions.Models.V1.FixAll
{
    [OmniSharpEndpoint(OmniSharpEndpoints.GetFixAll, typeof(GetFixAllRequest), typeof(GetFixAllResponse))]
    public class GetFixAllRequest: SimpleFileRequest
    {
        public FixAllScope Scope { get; set; } = FixAllScope.Document;
    }
}