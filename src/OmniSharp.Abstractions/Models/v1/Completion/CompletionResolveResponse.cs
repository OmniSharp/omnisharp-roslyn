#nullable enable

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OmniSharp.Models.v1.Completion
{
    public class CompletionResolveResponse
    {
        [JsonProperty("item")]
        public CompletionItem? Item { get; set; }
    }
}
