using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.TestCommand, typeof(TestCommandRequest), typeof(GetTestCommandResponse))]
    public class TestCommandRequest : Request
    {
        public TestCommandType Type { get; set; }
    }
}
