using Newtonsoft.Json;
namespace OmniSharp.Models.Navigate
{
    public class NavigateResponse
    {
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Line { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Column { get; set; }
    }
}
