using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

namespace OmniSharp.Models
{
    public class HighlightResponse
    {
        public LinePosition Start { get; set; }
        public LinePosition End { get; set; }
        public string Kind { get; set; }
        
        internal static HighlightResponse FromClassifiedSpan(ClassifiedSpan span, TextLineCollection lines)
        {
            var linePos = lines.GetLinePositionSpan(span.TextSpan);
            
            return new HighlightResponse
            {
                Start = linePos.Start,
                End = linePos.End,
                Kind = span.ClassificationType
            };
        }
    }
}
