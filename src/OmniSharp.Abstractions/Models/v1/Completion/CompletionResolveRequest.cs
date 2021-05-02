#nullable enable

using OmniSharp.Mef;

namespace OmniSharp.Models.v1.Completion
{
    [OmniSharpEndpoint(OmniSharpEndpoints.CompletionResolve, typeof(CompletionResolveRequest), typeof(CompletionResolveResponse))]
    public class CompletionResolveRequest : IRequest
    {
        public CompletionItem Item { get; set; } = null!;
    }
}
