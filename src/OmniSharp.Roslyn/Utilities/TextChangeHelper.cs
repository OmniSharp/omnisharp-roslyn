using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.Utilities
{
    internal static class TextChanges
    {
        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetAsync(Document document, Document oldDocument)
        {
            var changes = await document.GetTextChangesAsync(oldDocument);
            var oldText = await oldDocument.GetTextAsync();

            return Convert(oldText, changes);
        }

        public static IEnumerable<LinePositionSpanTextChange> Convert(SourceText oldText, params TextChange[] changes)
        {
            return Convert(oldText, (IEnumerable<TextChange>)changes);
        }

        public static IEnumerable<LinePositionSpanTextChange> Convert(SourceText oldText, IEnumerable<TextChange> changes)
        {
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

                        if (span.Start > 0 && newText[0] == '\n' && oldText[span.Start - 1] == '\r')
                        {
                            // text: foo\r\nbar\r\nfoo
                            // edit:      [----)
                            span = TextSpan.FromBounds(span.Start - 1, span.End);
                            prefix = "\r";
                        }
                        if (span.End < oldText.Length - 1 && newText[newText.Length - 1] == '\r' && oldText[span.End] == '\n')
                        {
                            // text: foo\r\nbar\r\nfoo
                            // edit:        [----)
                            span = TextSpan.FromBounds(span.Start, span.End + 1);
                            postfix = "\n";
                        }
                    }

                    var linePositionSpan = oldText.Lines.GetLinePositionSpan(span);

                    return new LinePositionSpanTextChange()
                    {
                        NewText = prefix + newText + postfix,
                        StartLine = linePositionSpan.Start.Line,
                        StartColumn = linePositionSpan.Start.Character,
                        EndLine = linePositionSpan.End.Line,
                        EndColumn = linePositionSpan.End.Character
                    };
                });
        }
    }
}
