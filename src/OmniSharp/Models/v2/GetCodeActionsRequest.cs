namespace OmniSharp.Models.V2
{
    public class GetCodeActionsRequest : Request, ICodeActionRequest
    {
        public Range Selection { get; set; }
    }
}
