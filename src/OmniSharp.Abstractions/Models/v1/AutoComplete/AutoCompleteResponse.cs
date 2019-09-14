
namespace OmniSharp.Models.AutoComplete
{
    public class AutoCompleteResponse
    {
        /// <summary>
        /// The text to be "completed", that is, the text that will be inserted in the editor.
        /// </summary>
        public string CompletionText { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// The text that should be displayed in the auto-complete UI.
        /// </summary>
        public string DisplayText { get; set; }
        public string RequiredNamespaceImport { get; set; }
        public string MethodHeader { get; set; }
        public string ReturnType { get; set; }
        public string Snippet { get; set; }
        public string Kind { get; set; }
        public bool IsSuggestionMode { get; set; }
        public bool Preselect { get; set; }

        public override bool Equals(object other)
        {
            var otherResponse = other as AutoCompleteResponse;
            return otherResponse.DisplayText == DisplayText
                && otherResponse.Snippet == Snippet;
        }

        public override int GetHashCode()
        {
            var hashCode = 17 * DisplayText.GetHashCode();

            if (Snippet != null)
            {
                hashCode += 31 * Snippet.GetHashCode();
            }

            return hashCode;
        }
    }
}
