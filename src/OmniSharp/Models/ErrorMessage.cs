using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class ErrorMessage
    {
        public string Text { get; set; }
        public string FileName { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }
}
