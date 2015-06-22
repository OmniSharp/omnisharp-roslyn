using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace OmniSharp.Models
{
    public class LinePositionSpanTextChange
    {
        public static async Task<IEnumerable<LinePositionSpanTextChange>> Convert(Document document, IEnumerable<TextChange> changes)
        {
            var text = await document.GetTextAsync();

            return changes
                .OrderByDescending(change => change.Span)
                .Select(change =>
                {
                    var span = change.Span;
                    var newText = change.NewText;
                    var prefix = string.Empty;
                    var postfix = string.Empty;

                    if (newText.Length > 0)
                    {
                        // Roslyn computes text changes on character arrays. So it might happen that a
                        // change starts inbetween \r\n which is OK when you are offset-based but a problem
                        // when you are line,column-based. This code extends text edits which just overlap
                        // a with a line break to its full line break

                        if (span.Start > 0 && newText[0] == '\n' && text[span.Start - 1] == '\r')
                        {
                            // text: foo\r\nbar\r\nfoo
                            // edit:      [----)
                            span = TextSpan.FromBounds(span.Start - 1, span.End);
                            prefix = "\r";
                        }
                        if (span.End < text.Length - 1 && newText[newText.Length - 1] == '\r' && text[span.End] == '\n')
                        {
                            // text: foo\r\nbar\r\nfoo
                            // edit:        [----)
                            span = TextSpan.FromBounds(span.Start, span.End + 1);
                            postfix = "\n";
                        }
                    }

                    var linePositionSpan = text.Lines.GetLinePositionSpan(span);
                    return new LinePositionSpanTextChange()
                    {
                        NewText = prefix + newText + postfix,
                        StartLine = linePositionSpan.Start.Line + 1,
                        StartColumn = linePositionSpan.Start.Character + 1,
                        EndLine = linePositionSpan.End.Line + 1,
                        EndColumn = linePositionSpan.End.Character + 1
                    };
                });
        }

        public string NewText { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as LinePositionSpanTextChange;
            if (other == null)
            {
                return false;
            }

            return NewText == other.NewText
                && StartLine == other.StartLine
                && StartColumn == other.StartColumn
                && EndLine == other.EndLine
                && EndColumn == other.EndColumn;
        }

        public override int GetHashCode()
        {
            return NewText.GetHashCode()
                * (23 + StartLine)
                * (29 + StartColumn)
                * (31 + EndLine)
                * (37 + EndColumn);
        }
    }

}
