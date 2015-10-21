namespace OmniSharp.Models
{
    public abstract class CodeActionRequest : Request
    {
        public int CodeAction { get; set; }
        public bool WantsTextChanges { get; set; }
        public int? SelectionStartColumn { get; set; }
        public int? SelectionStartLine { get; set; }
        public int? SelectionEndColumn { get; set; }
        public int? SelectionEndLine { get; set; }
    }
}
