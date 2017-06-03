namespace OmniSharp.Models.V2.Completion
{
    public class CompletionItem
    {
        public string DisplayText { get; set; }
        public string Kind { get; set; }
        public string FilterText { get; set; }
        public string SortText { get; set; }

        /// <summary>
        /// Rules that modify the set of characters that can be typed to cause the
        /// selected item to be commited.
        /// </summary>
        public CharacterSetModificationRule[] CommitCharacterRules { get; set; }

        // These properties must be resolved via the '/v2/completionItem/resolve'
        // end point before they are available.
        public string Description { get; set; }
        public TextEdit TextEdit { get; set; }
        public TextEdit[] AdditionalTextEdits { get; set; }
    }
}
