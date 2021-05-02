#nullable enable
using System.Collections.Immutable;

namespace OmniSharp.Models
{
    public class QuickInfoResponse
    {
        /// <summary>
        /// QuickInfo for the given position, rendered as markdown.
        /// </summary>
        public string Markdown { get; set; } = string.Empty;
    }
}
