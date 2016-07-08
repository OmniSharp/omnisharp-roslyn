namespace OmniSharp.DotNetTest.Models
{
    public class RunDotNetTestResponse
    {
        public bool Pass { get; set; }

        public string Failure { get; set; }
    }
}