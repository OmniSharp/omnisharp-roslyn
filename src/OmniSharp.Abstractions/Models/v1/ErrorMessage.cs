using System.Collections.Generic;
using Newtonsoft.Json;
using OmniSharp.Json;

namespace OmniSharp.Models
{
    public class ErrorMessage
    {
        public string Text { get; set; }
        public string FileName { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Line { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Column { get; set; }
    }
}
