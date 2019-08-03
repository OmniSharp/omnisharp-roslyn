using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Abstractions.Models.V1.FixAll
{
    [OmniSharpEndpoint(OmniSharpEndpoints.RunFixAll, typeof(RunFixAllRequest), typeof(RunFixAllResponse))]
    public class RunFixAllRequest : SimpleFileRequest
    {
        public FixAllScope Scope { get; set; } = FixAllScope.Document;
        public FixAllItem[] FixAllFilter { get; set; }
    }
}
