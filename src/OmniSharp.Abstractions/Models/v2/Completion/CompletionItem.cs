namespace OmniSharp.Models.V2.Completion
{
    public class CompletionItem
    {
        public string DisplayText { get; set; }
        public string Kind { get; set; }
        public string FilterText { get; set; }
        public string SortText { get; set; }

        public string Description { get; set; }
        public TextEdit TextEdit { get; set; }
        public TextEdit[] AdditionalTextEdits { get; set; }
    }
}
