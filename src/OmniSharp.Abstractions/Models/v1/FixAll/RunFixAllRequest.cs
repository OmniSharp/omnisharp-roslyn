using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Abstractions.Models.V1.FixAll
{
    [OmniSharpEndpoint(OmniSharpEndpoints.RunFixAll, typeof(RunFixAllRequest), typeof(RunFixAllResponse))]
    public class RunFixAllRequest : SimpleFileRequest
    {
        public FixAllScope Scope { get; set; } = FixAllScope.Document;

        // If this is null -> filter not set -> try to fix all issues in current defined scope.
        public FixAllItem[] FixAllFilter { get; set; }
        public int Timeout { get; set; } = 10000;
        public bool WantsAllCodeActionOperations { get; set; }
        public bool WantsTextChanges { get; set; }
        // Nullable for backcompat: null == true, for requests that don't set it
        public bool? ApplyChanges { get; set; }
    }
}
