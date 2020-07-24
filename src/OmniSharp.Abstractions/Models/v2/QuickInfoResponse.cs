#nullable enable
namespace OmniSharp.Models.v2
{
    public class QuickInfoResponse
    {
        /// <summary>
        /// Description of the symbol under the cursor. This is expected to be rendered as a C# codeblock
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Documentation of the symbol under the cursor, if present. It is expected to be rendered as markdown.
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Other relevant information to the symbol under the cursor.
        /// </summary>
        public QuickInfoResponseSection[]? RemainingSections { get; set; }
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
