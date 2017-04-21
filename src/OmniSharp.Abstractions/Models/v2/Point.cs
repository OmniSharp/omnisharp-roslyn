using Newtonsoft.Json;

namespace OmniSharp.Models.V2
{
    public class Point
    {
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Line { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Column { get; set; }
    }
}
