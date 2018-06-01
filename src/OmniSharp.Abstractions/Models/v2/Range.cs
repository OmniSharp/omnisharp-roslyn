namespace OmniSharp.Models.V2
{
    public class Range
    {
        public Point Start { get; set; }
        public Point End { get; set; }

        public override string ToString()
            => $"Start = {{{Start}}}, End = {{{End}}}";

        public bool Contains(int line, int column)
        {
            if (Start.Line > line || End.Line < line)
            {
                return false;
            }

            if (Start.Line == line && Start.Column > column)
            {
                return false;
            }

            if (End.Line == line && End.Column < column)
            {
                return false;
            }

            return true;
        }
    }
}
