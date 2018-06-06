using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace OmniSharp.Models.V2
{
    public class Point : IEquatable<Point>
    {
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Line { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Column { get; set; }

        public override bool Equals(object obj)
            => Equals(obj as Point);

        public bool Equals(Point other)
            => other != null
                && Line == other.Line
                && Column == other.Column;

        public override int GetHashCode()
        {
            var hashCode = -1456208474;
            hashCode = hashCode * -1521134295 + Line.GetHashCode();
            hashCode = hashCode * -1521134295 + Column.GetHashCode();
            return hashCode;
        }

        public override string ToString()
            => $"Line = {Line}, Column = {Column}";

        public static bool operator ==(Point point1, Point point2)
            => EqualityComparer<Point>.Default.Equals(point1, point2);

        public static bool operator !=(Point point1, Point point2)
            => !(point1 == point2);
    }
}
