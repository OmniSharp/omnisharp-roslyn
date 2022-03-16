using OmniSharp.Models.V2;

#nullable enable annotations

namespace OmniSharp.Models.v1.InlayHints;

public sealed record InlayHint
{
    public Point Position { get; set; }
    public string Label { get; set; }
    public string? Tooltip { get; set; }
    public (string SolutionVersion, int Position) Data { get; set; }

#nullable enable
    public override string ToString()
    {
        return $"InlineHint {{ {nameof(Position)} = {Position}, {nameof(Label)} = {Label}, {nameof(Tooltip)} = {Tooltip} }}";
    }

    public bool Equals(InlayHint? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        return Position == other.Position && Label == other.Label && Tooltip == other.Tooltip;
    }

    public override int GetHashCode() => (Position, Label, Tooltip).GetHashCode();
}

public enum InlayHintKind
{
    Type = 1,
    Parameter = 2,
}
