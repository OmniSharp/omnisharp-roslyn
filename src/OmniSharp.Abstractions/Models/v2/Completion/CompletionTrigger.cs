using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OmniSharp.Models.V2.Completion
{
    public class CompletionTrigger
    {
        /// <summary>
        /// The action that started completion.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter), /*camelCaseText*/ true)]
        public CompletionTriggerKind Kind { get; set; }

        /// <summary>
        /// The character associated with the triggering action.
        /// </summary>
        public string Character { get; set; }
    }
}
