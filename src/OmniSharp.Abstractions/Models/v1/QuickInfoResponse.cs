#nullable enable
using System.Collections.Immutable;

namespace OmniSharp.Models
{
    public class QuickInfoResponse
    {
        /// <summary>
        /// Other relevant information to the symbol under the cursor.
        /// </summary>
        public ImmutableArray<QuickInfoResponseSection> Sections { get; set; }
    }

    public struct QuickInfoResponseSection
    {
        /// <summary>
        /// If true, the text should be rendered as C# code. If false, the text should be rendered as markdown.
        /// </summary>
        public bool IsCSharpCode { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            return $@"{{ IsCSharpCode = {IsCSharpCode}, Text = ""{Text}"" }}";
        }
    }
}
