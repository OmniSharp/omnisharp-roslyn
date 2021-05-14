#nullable enable

using System;
using System.Collections.Generic;

namespace OmniSharp.Models.v1.Completion
{
    public class CompletionItem
    {
        /// <summary>
        /// The label of this completion item. By default
        /// also the text that is inserted when selecting
        /// this completion.
        /// </summary>
        public string Label { get; set; } = null!;

        /// <summary>
        /// The kind of this completion item. Based of the kind
        /// an icon is chosen by the editor.The standardized set
        /// of available values is defined in <see cref="CompletionItemKind"/>
        /// </summary>
        public CompletionItemKind Kind { get; set; }

        /// <summary>
        /// Tags for this completion item
        /// </summary>
        public IReadOnlyList<CompletionItemTag>? Tags { get; set; }

        /// <summary>
        /// A human-readable string with additional information
        /// about this item, like type or symbol information
        /// </summary>
        public string? Detail { get; set; }

        /// <summary>
        /// A human-readable string that represents a doc-comment. This is
        /// formatted as markdown.
        /// </summary>
        public string? Documentation { get; set; }

        /// <summary>
        /// Select this item when showing.
        /// </summary>
        public bool Preselect { get; set; }

        /// <summary>
        /// A string that should be used when comparing this item
        /// with other items. When null or empty the label is used.
        /// </summary>
        public string? SortText { get; set; }

        /// <summary>
        /// A string that should be used when filtering a set of
        /// completion items. When null or empty the label is used.
        /// </summary>
        public string? FilterText { get; set; }

        /// <summary>
        /// The format of <see cref="InsertText"/>. This applies to both <see cref="InsertText"/> and
        /// <see cref="TextEdit"/>.<see cref="LinePositionSpanTextChange.NewText"/>.
        /// </summary>
        public InsertTextFormat InsertTextFormat { get; set; }

        /// <summary>
        /// An edit which is applied to a document when selecting this completion. When an edit is provided the value of
        /// <see cref="InsertText"/> is ignored.
        /// </summary>
        public LinePositionSpanTextChange? TextEdit { get; set; }

        /// <summary>
        /// An optional set of characters that when pressed while this completion is active will accept it first and
        /// then type that character.
        /// </summary>
        public IReadOnlyList<char>? CommitCharacters { get; set; }

        /// <summary>
        /// An optional array of additional text edits that are applied when
        /// selecting this completion.Edits must not overlap (including the same insert position)
        /// with the main edit nor with themselves.
        ///
        /// Additional text edits should be used to change text unrelated to the current cursor position
        /// (for example adding an import statement at the top of the file if the completion item will
        /// insert an unqualified type).
        /// </summary>
        public IReadOnlyList<LinePositionSpanTextChange>? AdditionalTextEdits { get; set; }

        /// <summary>
        /// Index in the completions list that this completion occurred.
        /// </summary>
        public (long CacheId, int Index) Data { get; set; }

        /// <summary>
        /// True if there is a post-insert step for this completion item for asynchronous completion support.
        /// </summary>
        public bool HasAfterInsertStep { get; set; }

        public override string ToString()
        {
            return $"{{ {nameof(Label)} = {Label}, {nameof(CompletionItemKind)} = {Kind} }}";
        }
    }

    public enum CompletionItemKind
    {
        Text = 1,
        Method = 2,
        Function = 3,
        Constructor = 4,
        Field = 5,
        Variable = 6,
        Class = 7,
        Interface = 8,
        Module = 9,
        Property = 10,
        Unit = 11,
        Value = 12,
        Enum = 13,
        Keyword = 14,
        Snippet = 15,
        Color = 16,
        File = 17,
        Reference = 18,
        Folder = 19,
        EnumMember = 20,
        Constant = 21,
        Struct = 22,
        Event = 23,
        Operator = 24,
        TypeParameter = 25,
    }

    public enum CompletionItemTag
    {
        Deprecated = 1,
    }

    public enum InsertTextFormat
    {
        PlainText = 1,
        // TODO: Support snippets
        Snippet = 2,
    }
}
