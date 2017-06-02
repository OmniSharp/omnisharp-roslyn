using OmniSharp.Mef;

namespace OmniSharp.Models.V2.Completion
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.CompletionItemResolve, typeof(CompletionItemResolveRequest), typeof(CompletionItemResolveResponse))]
    public class CompletionItemResolveRequest : FileBasedRequest
    {
        /// <summary>
        /// Zero-based index of the completion item to resolve within the list of items returned
        /// by the last <see cref="CompletionResponse"/>.
        /// </summary>
        public int ItemIndex { get; set; }

        /// <summary>
        /// The display text of the completion item to resolve. If set, this is used to help verify
        /// that the correct completion item is resolved.
        /// </summary>
        public string DisplayText { get; set; }
    }
}
