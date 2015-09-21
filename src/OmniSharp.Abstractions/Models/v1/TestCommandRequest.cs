using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/gettestcontext", typeof(TestCommandRequest), typeof(GetTestCommandResponse))]
    public class TestCommandRequest : Request
    {
        public TestCommandType Type { get; set; }
    }
}
