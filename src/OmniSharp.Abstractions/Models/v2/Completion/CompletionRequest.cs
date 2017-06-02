using OmniSharp.Mef;

namespace OmniSharp.Models.V2.Completion
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.Completion, typeof(CompletionRequest), typeof(CompletionResponse))]
    public class CompletionRequest
    {
        public string FileName { get; set; }
        public int Position { get; set; }
    }
}
