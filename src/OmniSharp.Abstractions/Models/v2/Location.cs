#nullable enable

namespace OmniSharp.Models.V2
{
    public record Location
    {
        public string FileName { get; init; } = null!;
        public Range Range { get; init; } = null!;
    }
}
