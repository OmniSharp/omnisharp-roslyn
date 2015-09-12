
namespace OmniSharp.Models
{
    public class AutoCompleteResponse
    {
        public string CompletionText { get; set; }
        public string Description { get; set; }
        public string DisplayText { get; set; }
        public string RequiredNamespaceImport { get; set; }
        public string MethodHeader { get; set; }
        public string ReturnType { get; set; }
        public string Snippet { get; set; }
        public string Kind { get; set; }

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