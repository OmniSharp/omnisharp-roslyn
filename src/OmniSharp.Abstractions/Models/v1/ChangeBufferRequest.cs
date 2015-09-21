
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/changebuffer", typeof(ChangeBufferRequest), typeof(object))]
    public class ChangeBufferRequest : IRequest
    {
        public string FileName { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public string NewText { get; set; }
    }
}
