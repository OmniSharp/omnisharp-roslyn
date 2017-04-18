using OmniSharp.Mef;

namespace OmniSharp.Models.TestCommand
{
    [OmniSharpEndpoint(OmniSharpEndpoints.TestCommand, typeof(TestCommandRequest), typeof(GetTestCommandResponse))]
    public class TestCommandRequest : Request
    {
        public TestCommandType Type { get; set; }
    }
}
