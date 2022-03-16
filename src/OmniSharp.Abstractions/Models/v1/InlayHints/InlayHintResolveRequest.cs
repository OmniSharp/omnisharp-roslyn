using OmniSharp.Mef;

namespace OmniSharp.Models.v1.InlayHints;

[OmniSharpEndpoint(OmniSharpEndpoints.InlayHintResolve, typeof(InlayHintResolveRequest), typeof(InlayHint))]
public record InlayHintResolveRequest : IRequest
{
    public InlayHint Hint { get; set; }
}
