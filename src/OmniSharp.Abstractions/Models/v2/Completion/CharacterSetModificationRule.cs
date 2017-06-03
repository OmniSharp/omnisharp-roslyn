using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OmniSharp.Models.V2.Completion
{
    public class CharacterSetModificationRule
    {
        /// <summary>
        /// The kind of modification.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter), /*camelCaseText*/ true)]
        public CharacterSetModificationRuleKind Kind { get; set; }

        /// <summary>
        /// One or more characters.
        /// </summary>
        public char[] Characters { get; set; }
    }
}
