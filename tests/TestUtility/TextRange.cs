using System;

namespace TestUtility
{
    public struct TextRange : IEquatable<TextRange>, IComparable<TextRange>
    {
        public readonly TextPoint Start;
        public readonly TextPoint End;

        public bool IsEmpty => this.Start == this.End;

        public TextRange(TextPoint start, TextPoint end)
        {
            this.Start = start;
            this.End = end;
        }

        public int CompareTo(TextRange other)
        {
            if (this.Start < other.Start)
            {
                return -1;
            }
            else if (this.Start > other.Start)
            {
                return 1;
            }
            else if (this.End < other.End)
            {
                return -1;
            }
            else if (this.End > other.End)
            {
                return 1;
            }

            return 0;
        }

        public override bool Equals(object obj)
            => obj is TextRange && Equals((TextRange)obj);

        public bool Equals(TextRange other)
            => this.Start == other.Start && this.End == other.End;

        public override int GetHashCode()
            => this.Start.GetHashCode() ^ this.End.GetHashCode();

        public override string ToString()
            => $"{{Start={this.Start}, End={this.End}}}";

        public static bool operator ==(TextRange range11, TextRange range12)
            => range11.Equals(range12);

        public static bool operator !=(TextRange range11, TextRange range12)
            => !range11.Equals(range12);

        public static bool operator <(TextRange range11, TextRange range12)
            => range11.CompareTo(range12) < 0;

        public static bool operator <=(TextRange range11, TextRange range12)
            => range11.CompareTo(range12) <= 0;

        public static bool operator >(TextRange range11, TextRange range12)
            => range11.CompareTo(range12) > 0;

        public static bool operator >=(TextRange range11, TextRange range12)
            => range11.CompareTo(range12) >= 0;
    }
}
