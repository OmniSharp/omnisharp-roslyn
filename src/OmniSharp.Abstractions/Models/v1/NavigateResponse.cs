using Newtonsoft.Json;
using OmniSharp.Json;

namespace OmniSharp.Models
{
    public class NavigateResponse
    {
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Line { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Column { get; set; }
    }
}
