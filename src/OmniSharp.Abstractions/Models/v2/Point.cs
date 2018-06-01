using Newtonsoft.Json;

namespace OmniSharp.Models.V2
{
    public class Point
    {
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Line { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Column { get; set; }

        public override bool Equals(object obj)
        {
            var point = obj as Point;
            return point != null &&
                   Line == point.Line &&
                   Column == point.Column;
        }

        public override int GetHashCode()
        {
            var hashCode = -1456208474;
            hashCode = hashCode * -1521134295 + Line.GetHashCode();
            hashCode = hashCode * -1521134295 + Column.GetHashCode();
            return hashCode;
        }
    }
}
