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
        public static async Task<IEnumerable<LinePositionSpanTextChange>> GetAsync(Document document, Document oldDocument, int? originalEnd = null)
        {
            var changes = await document.GetTextChangesAsync(oldDocument);
            var oldText = await oldDocument.GetTextAsync();

            return Convert(oldText, changes, originalEnd);
        }

        public static IEnumerable<LinePositionSpanTextChange> Convert(SourceText oldText, params TextChange[] changes)
        {
            return Convert(oldText, (IEnumerable<TextChange>)changes);
        }

        public static IEnumerable<LinePositionSpanTextChange> Convert(SourceText oldText, IEnumerable<TextChange> changes, int? originalEnd = null)
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
                        // Due to https://github.com/dotnet/roslyn/issues/50129, we might get a text change
                        // that extends further than the requested end bound. This doesn't matter for every
                        // scenario, but is annoying when hitting enter and having a trailing space on the previous
                        // line, as that will end up removing the newly-added indentation on the current line.
                        // For the cases that matters, originalEnd will be not null, and we reduce the end of the span
                        // to be that location.
                        if (originalEnd is int oEnd && span.Start < oEnd && span.End > oEnd)
                        {
                            // Since the new change is beyond the requested line, it's also going to end in either a
                            // \r\n or \n, which replaces the newline on the current line. To avoid that newline
                            // becoming an extra blank line, we extend the span an addition 2 or 1 characters to replace
                            // that point.
                            int newLength = span.Length - (span.End - oEnd);
                            if (newText[newText.Length - 1] == '\n')
                            {
                                // The new text ends with a newline. Check the original text to see what type of newline
                                // it used at the end location and increase by that amount
                                newLength += oldText[oEnd] switch
                                {
                                    '\r' => 2,
                                    '\n' => 1,
                                    _ => 0
                                };
                            }

                            span = new TextSpan(span.Start, newLength);
                        }


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
                }).ToList();
        }
    }
}
