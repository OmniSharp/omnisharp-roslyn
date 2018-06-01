namespace OmniSharp.Models.V2.CodeStructure
{
    public class CodeElementRange
    {
        public string Name { get; set; }
        public Range Range { get; set; }

        public override string ToString()
            => $"{Name} = {{{Range}}}";
    }
}
