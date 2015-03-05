using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Models
{
    public class TextChange
    {
        public static async Task<IEnumerable<TextChange>> Convert(Document document, IEnumerable<Microsoft.CodeAnalysis.Text.TextChange> changes)
        {
            var lines = (await document.GetTextAsync()).Lines;
            
            return changes.Select(change =>
            {
                var linePositionSpan = lines.GetLinePositionSpan(change.Span);

                return new Models.TextChange()
                {
                    NewText = change.NewText,
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
    }
}
