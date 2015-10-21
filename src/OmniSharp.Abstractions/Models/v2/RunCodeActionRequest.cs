using OmniSharp.Mef;

namespace OmniSharp.Models.V2
{
    [OmniSharpEndpoint(OmnisharpEndpoints.V2.RunCodeAction, typeof(RunCodeActionRequest), typeof(RunCodeActionResponse))]
    public class RunCodeActionRequest : Request, ICodeActionRequest
    {
        public string Identifier { get; set; }
        public Range Selection { get; set; }
        public bool WantsTextChanges { get; set; }
    }
}
