using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class ModifiedFileResponse : FileOperationResponse
    {
        public ModifiedFileResponse(string fileName)
            : base(fileName, FileModificationType.Modified)
        {
        }

        public string Buffer { get; set; }
        public IEnumerable<LinePositionSpanTextChange> Changes { get; set; }
    }
}
