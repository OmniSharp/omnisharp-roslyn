using OmniSharp.Mef;

namespace OmniSharp.Models.V2.Completion
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.Completion, typeof(CompletionRequest), typeof(CompletionResponse))]
    public class CompletionRequest : IRequest
    {
        /// <summary>
        /// The name of the file in which completion is requested.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// The zero-based position in the file where completion is requested.
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// The action that started completion.
        /// </summary>
        public CompletionTrigger Trigger { get; set; }
    }
}
