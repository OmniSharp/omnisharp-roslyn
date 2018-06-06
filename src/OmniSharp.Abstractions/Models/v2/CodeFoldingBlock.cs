namespace OmniSharp.Models.V2
{
    public class CodeFoldingBlock
    {
        public CodeFoldingBlock(Range textSpan, string type)
        {
            Range = textSpan;
            Type = type;
        }

        /// <summary>
        /// The span of text to collapse.
        /// </summary>
        public Range Range { get; }

        /// <summary>
        /// If the block is one of the types specified in <see cref="CodeFoldingBlockKind"/>, that type.
        /// Otherwise, null.
        /// </summary>
        public string Type { get; }
    }

    public class CodeFoldingBlockKind
    {
        public static readonly string Comment = nameof(Comment);
        public static readonly string Imports = nameof(Imports);
        public static readonly string Region = nameof(Region);
    }
}
