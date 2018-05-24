namespace OmniSharp.DotNetTest.Models
{
    public class DotNetTestResult
    {
        public string MethodName { get; set; }
        public string Outcome { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorStackTrace { get; set; }
        public string[] StdOutMessages {get; set; }
    }
}
