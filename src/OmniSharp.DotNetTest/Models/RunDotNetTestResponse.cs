namespace OmniSharp.DotNetTest.Models
{
    public class RunDotNetTestResponse
    {
        public DotNetTestResult[] Results { get; set; }
        public bool Pass { get; set; }
        public string Failure { get; set; }
    }
}