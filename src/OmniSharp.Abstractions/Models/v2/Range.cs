namespace OmniSharp.Models.V2
{
    public record Range
    {
        public Point Start { get; init; }
        public Point End { get; init; }

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

        public bool IsValid() => Start != null && Start.Line > -1 && Start.Column > -1 && End != null && End.Line > -1 && End.Column > -1;
    }
}
