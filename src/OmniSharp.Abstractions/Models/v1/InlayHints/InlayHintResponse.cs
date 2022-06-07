#nullable enable annotations

using System.Collections.Generic;

namespace OmniSharp.Models.v1.InlayHints;

public record InlayHintResponse
{
    public static readonly InlayHintResponse None = new() { InlayHints = new() };
    public List<InlayHint> InlayHints { get; set; }
}
