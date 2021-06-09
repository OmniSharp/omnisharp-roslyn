using OmniSharp.Mef;

namespace OmniSharp.Models.V2.CodeActions
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.RunCodeAction, typeof(RunCodeActionRequest), typeof(RunCodeActionResponse))]
    public class RunCodeActionRequest : Request, ICodeActionRequest
    {
        public string Identifier { get; set; }
        public Range Selection { get; set; }
        public bool WantsTextChanges { get; set; }
        public bool ApplyTextChanges { get; set; } = true;
        public bool WantsAllCodeActionOperations { get; set; }

        public ICodeActionRequest WithSelection(Range newSelection) => new RunCodeActionRequest
        {
            Line = this.Line,
            Column = this.Column,
            Buffer = this.Buffer,
            ApplyChangesTogether = this.ApplyChangesTogether,
            Changes = this.Changes,
            FileName = this.FileName,
            Identifier = this.Identifier,
            WantsTextChanges = this.WantsTextChanges,
            ApplyTextChanges = this.ApplyTextChanges,
            WantsAllCodeActionOperations = this.WantsAllCodeActionOperations,
            Selection = newSelection
        };
    }
}
