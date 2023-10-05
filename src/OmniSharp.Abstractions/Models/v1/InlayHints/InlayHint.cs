using OmniSharp.Models.V2;

#nullable enable annotations

namespace OmniSharp.Models.v1.InlayHints;

public sealed record InlayHint
{
    /// <summary>
    /// The position of this hint.
    /// </summary>
    public Point Position { get; set; }

    /// <summary>
    /// The label of this hint. A human readable string.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// The kind of this hint. Can be omitted in which case the client
	/// should fall back to a reasonable default.
    /// </summary>
    public InlayHintKind? Kind { get; set; }

    /// <summary>
    /// The tooltip text when you hover over this item.
    /// </summary>
    public string? Tooltip { get; set; }

    /// <summary>
    /// Optional text edits that are performed when accepting this inlay hint.
    /// </summary>
    public LinePositionSpanTextChange[]? TextEdits { get; set; }

    /// <summary>
    /// A data entry field that is preserved on a inlay hint between a <see cref="InlayHintRequest" /> and a <see cref="InlayHintResolveRequest" />.
    /// </summary>
    public (string SolutionVersion, int Position) Data { get; set; }

#nullable enable
    public override string ToString()
    {
        var textEdits = TextEdits is null ? "null" : $"[ {string.Join<LinePositionSpanTextChange>(", ", TextEdits)} ]";
        return $"InlayHint {{ {nameof(Position)} = {Position}, {nameof(Label)} = {Label}, {nameof(Tooltip)} = {Tooltip ?? "null"}, {nameof(TextEdits)} = {textEdits} }}";
    }

    public bool Equals(InlayHint? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        return Position == other.Position
            && Label == other.Label
            && Tooltip == other.Tooltip
            && TextEditsEqual(TextEdits, other.TextEdits);
    }

    private static bool TextEditsEqual(LinePositionSpanTextChange[]? a, LinePositionSpanTextChange[]? b)
    {
        if (a is null)
        {
            return b is null;
        }

        if (b is null)
        {
            return false;
        }

        if (a.Length != b.Length)
        {
            return false;
        }

        for (int index = 0; index < a.Length; index++)
        {
            if (!a[index].Equals(b[index]))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode() => (Position, Label, Tooltip, TextEdits?.GetHashCode() ?? 0).GetHashCode();
}

public enum InlayHintKind
{
    /// <summary>
    /// An inlay hint that is for a type annotation.
    /// </summary>
    Type = 1,
    /// <summary>
    /// An inlay hint that is for a parameter.
    /// </summary>
    Parameter = 2,
}
