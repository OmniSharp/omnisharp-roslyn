namespace OmniSharp.Models.V2
{
    public class CodeFoldingBlock
    {
        public CodeFoldingBlock(Range textSpan, string type)
        {
            Range = textSpan;
            Kind = type;
        }

        /// <summary>
        /// The span of text to collapse.
        /// </summary>
        public Range Range { get; }

        /// <summary>
        /// If the block is one of the types specified in <see cref="CodeFoldingBlockKinds"/>, that type.
        /// Otherwise, null.
        /// </summary>
        public string Kind { get; }
    }

    public class CodeFoldingBlockKinds
    {
        public static readonly string Comment = nameof(Comment);
        public static readonly string Imports = nameof(Imports);
        public static readonly string Region = nameof(Region);
    }
}
