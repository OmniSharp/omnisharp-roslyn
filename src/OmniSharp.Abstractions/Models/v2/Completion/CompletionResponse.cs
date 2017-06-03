namespace OmniSharp.Models.V2.Completion
{
    public class CompletionResponse
    {
        // Note: We use an expression-bodied property rather than a getter-only auto-property
        // to ensure that a new instance is created every time, since this class is mutable.
        public static CompletionResponse Empty => new CompletionResponse();

        /// <summary>
        /// The default set of typed characters that cause a completion item to be committed.
        /// </summary>
        public char[] DefaultCommitCharacters { get; set; }

        /// <summary>
        /// Returns true if the completion list is "suggestion mode", meaning that it should not
        /// commit aggressively on characters like ' '.
        /// </summary>
        public bool IsSuggestionMode { get; set; }

        /// <summary>
        /// The completion items to present to the user.
        /// </summary>
        public CompletionItem[] Items { get; set; }
    }
}
