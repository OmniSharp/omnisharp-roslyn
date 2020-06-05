namespace OmniSharp.DotNetTest.Models
{
    public class RunTestResponse
    {
        public DotNetTestResult[] Results { get; set; }
        public bool Pass { get; set; }
        public string Failure { get; set; }
        public bool ContextHadNoTests { get; set; }
    }
}
