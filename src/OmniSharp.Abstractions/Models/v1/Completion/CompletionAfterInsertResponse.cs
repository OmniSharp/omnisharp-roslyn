#nullable enable

using Newtonsoft.Json;
using System.Collections.Generic;

namespace OmniSharp.Models.v1.Completion
{
    public class CompletionAfterInsertResponse
    {
        /// <summary>
        /// Text changes to be applied to the document. These need to be applied in batch, all with reference to
        /// the same original document.
        /// </summary>
        public IReadOnlyList<LinePositionSpanTextChange>? Changes { get; set; }
        /// <summary>
        /// Line to position the cursor on after applying <see cref="Changes"/>.
        /// </summary>
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int? Line { get; set; }
        /// <summary>
        /// Column to position the cursor on after applying <see cref="Changes"/>.
        /// </summary>
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int? Column { get; set; }
    }
}
