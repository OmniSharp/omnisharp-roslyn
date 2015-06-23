namespace OmniSharp.Models.V2
{
    public class RunCodeActionRequest : Request, ICodeActionRequest
    {
        public string Identifier { get; set; }
        public Range Selection { get; set; }
        public bool WantsTextChanges { get; set; }
    }
}
