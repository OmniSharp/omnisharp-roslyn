namespace OmniSharp.Models
{
    public class GotoDefinitionRequest : Request
    {
        public int Timeout { get; set; } = 2000;
    }
}
