namespace OmniSharp.DotNetTest.Models.Events
{
    public class TestMessageEvent
    {
        public const string Id = "TestMessage";

        public string MessageLevel { get; set; }
        public string Message { get; set; }
    }
}
