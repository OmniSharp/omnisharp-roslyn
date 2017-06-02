namespace OmniSharp.Models.V2.Completion
{
    public class CompletionResponse
    {
        public bool IsSuggestionMode { get; set; }
        public CompletionItem[] Items { get; set; }
    }
}
