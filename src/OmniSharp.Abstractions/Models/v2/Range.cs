namespace OmniSharp.Models.V2
{
    public class Range
    {
        public Point Start { get; set; }
        public Point End { get; set; }

        public override string ToString()
            => $"Start = {{{Start}}}, End = {{{End}}}";
    }
}
