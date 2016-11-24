using System;

namespace TestUtility
{
    public struct TextPoint : IEquatable<TextPoint>, IComparable<TextPoint>
    {
        public readonly int Line;

        // NOTE: This is intentionally called offset. Traditionally, "columns" are computed after tabs are expanded.
        public readonly int Offset;

        public TextPoint(int line, int offset)
        {
            this.Line = line;
            this.Offset = offset;
        }

        public int CompareTo(TextPoint other)
        {
            if (this.Line < other.Line)
            {
                return -1;
            }
            else if (this.Line > other.Line)
            {
                return 1;
            }
            else if (this.Offset < other.Offset)
            {
                return -1;
            }
            else if (this.Offset > other.Offset)
            {
                return 1;
            }

            return 0;
        }

        public override bool Equals(object obj)
            => obj is TextPoint && Equals((TextPoint)obj);

        public bool Equals(TextPoint other)
            => this.Line == other.Line && this.Offset == other.Line;

        public override int GetHashCode()
            => this.Line ^ this.Offset;

        public override string ToString()
            => $"{{Line={this.Line}, Offset={this.Offset}}}";

        public static bool operator ==(TextPoint point1, TextPoint point2)
            => point1.Equals(point2);

        public static bool operator !=(TextPoint point1, TextPoint point2)
            => !point1.Equals(point2);

        public static bool operator <(TextPoint point1, TextPoint point2)
            => point1.CompareTo(point2) < 0;

        public static bool operator <=(TextPoint point1, TextPoint point2)
            => point1.CompareTo(point2) <= 0;

        public static bool operator >(TextPoint point1, TextPoint point2)
            => point1.CompareTo(point2) > 0;

        public static bool operator >=(TextPoint point1, TextPoint point2)
            => point1.CompareTo(point2) >= 0;
    }
}
