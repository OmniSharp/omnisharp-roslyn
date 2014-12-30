namespace OmniSharp.Models
{
    public class TestCommandRequest : Request
    {
        public TestCommandType Type { get; set; }
        public enum TestCommandType
        {
            All, Fixture, Single
        }
    }
}