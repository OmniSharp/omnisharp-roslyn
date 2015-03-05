using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class ModifiedFileResponse
    {
        public ModifiedFileResponse() { }

        public ModifiedFileResponse(string fileName)
        {
            FileName = fileName;
        }

        public string FileName { get; set; }
        public string Buffer { get; set; }
        public IEnumerable<TextChange> Changes { get; set; }
    }
}