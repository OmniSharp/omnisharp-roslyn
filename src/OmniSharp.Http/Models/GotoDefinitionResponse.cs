namespace OmniSharp.Models
{
    public class GotoDefinitionResponse
    {
        public string FileName { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }
}