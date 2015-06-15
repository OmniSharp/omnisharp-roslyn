namespace OmniSharp.Models
{
    public class CodeActionRequest : Request
    {
        public int CodeAction { get; set; }
        public bool WantsTextChanges { get; set; }
        public int? SelectionStartColumn { get; set; }
        public int? SelectionStartLine { get; set; }
        public int? SelectionEndColumn { get; set; }
        public int? SelectionEndLine { get; set; }

        public bool HasRange
        {
            get
            {
                return SelectionStartColumn.HasValue &&
                    SelectionEndColumn.HasValue &&
                    SelectionStartLine.HasValue &&
                    SelectionEndLine.HasValue;
            }
        }
    }
}
