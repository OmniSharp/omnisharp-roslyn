using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class GotoDefinitionResponse
    {
        public string FileName { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string MetadataSource { get; set; }
    }
}