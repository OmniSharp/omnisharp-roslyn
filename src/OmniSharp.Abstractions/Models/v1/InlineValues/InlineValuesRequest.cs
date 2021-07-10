#nullable enable

using OmniSharp.Mef;
using OmniSharp.Models.V2;

namespace OmniSharp.Models.v1.InlineValues
{
    [OmniSharpEndpoint(OmniSharpEndpoints.InlineValues, typeof(InlineValuesRequest), typeof(InlineValuesResponse))]
    public class InlineValuesRequest : SimpleFileRequest
    {
        public Range ViewPort { get; init; } = null!;
        public InlineValuesContext Context { get; init; } = null!;
    }

    public class InlineValuesContext
    {
        public int FrameId { get; init; }
        public Range StoppedLocation { get; init; } = null!;
    }
}
