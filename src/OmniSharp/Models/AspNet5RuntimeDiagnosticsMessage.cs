using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class AspNet5RuntimeDiagnosticsMessage
    {
        public string Text { get; set; }
        public IEnumerable<string> SearchLocations { get; set; }
        public string FileName { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }
}
