using OmniSharp.Models.v1.SourceGeneratedFile;

namespace OmniSharp.Models
{
    public class SymbolLocation : QuickFix
    {
        public string Kind { get; set; }
        public string ContainingSymbolName { get; set; }
        public SourceGeneratedFileInfo GeneratedFileInfo { get; set; }
    }
}
