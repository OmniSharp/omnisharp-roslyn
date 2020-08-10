#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace OmniSharp.Models.v1.Completion
{
    public class CompletionResponse
    {
        /// <summary>
        /// If true, this list is not complete. Further typing should result in recomputing the list.
        /// </summary>
        [JsonProperty("isIncomplete")]
        public bool IsIncomplete { get; set; }

        /// <summary>
        /// The completion items.
        /// </summary>
        [JsonProperty("items")]
        public ImmutableArray<CompletionItem> Items { get; set; }
    }
}
