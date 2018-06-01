using System.Collections.Generic;

namespace OmniSharp.Models.V2
{
    public class Range
    {
        public Point Start { get; set; }
        public Point End { get; set; }

        public override bool Equals(object obj)
        {
            var range = obj as Range;
            return range != null &&
                   EqualityComparer<Point>.Default.Equals(Start, range.Start) &&
                   EqualityComparer<Point>.Default.Equals(End, range.End);
        }

        public override int GetHashCode()
        {
            var hashCode = -1676728671;
            hashCode = hashCode * -1521134295 + EqualityComparer<Point>.Default.GetHashCode(Start);
            hashCode = hashCode * -1521134295 + EqualityComparer<Point>.Default.GetHashCode(End);
            return hashCode;
        }
    }
}
