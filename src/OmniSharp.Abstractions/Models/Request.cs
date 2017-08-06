using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace OmniSharp.Models
{
    public class Request : IRequest
    {
        private string _fileName;

        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Line { get; set; }
        [JsonConverter(typeof(ZeroBasedIndexConverter))]
        public int Column { get; set; }
        public string Buffer { get; set; }
        public IEnumerable<LinePositionSpanTextChange> Changes { get; set; }
        public string FileName
        {
            get
            {
                return _fileName?.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
            set
            {
                _fileName = value;
            }
        }
    }
}
