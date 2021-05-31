using OmniSharp.Mef;

namespace OmniSharp.Models.V2.CodeActions
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.GetCodeActions, typeof(GetCodeActionsRequest), typeof(GetCodeActionsResponse))]
    public class GetCodeActionsRequest : Request, ICodeActionRequest
    {
        public Range Selection { get; set; }

        public ICodeActionRequest WithSelection(Range newSelection) => new GetCodeActionsRequest
        {
            Line = this.Line,
            Column = this.Column,
            Buffer = this.Buffer,
            ApplyChangesTogether = this.ApplyChangesTogether,
            Changes = this.Changes,
            FileName = this.FileName,
            Selection = newSelection
        };
    }
}
