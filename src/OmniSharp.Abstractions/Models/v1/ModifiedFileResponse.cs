using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OmniSharp.Models
{
    public enum FileModificationType
    {
        Modified,
        Opened,
        Renamed
    }

    public class ModifiedFileResponse
    {
        public ModifiedFileResponse() { }

        public ModifiedFileResponse(string fileName, FileModificationType type)
        {
            FileName = fileName;
            ModificationType = type;
        }

        public string FileName { get; set; }
        public string Buffer { get; set; }
        public IEnumerable<LinePositionSpanTextChange> Changes { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public FileModificationType ModificationType { get; }
    }

    public class RenamedFileResponse : ModifiedFileResponse
    {
        public RenamedFileResponse(string fileName, string newFileName)
            : base(fileName, FileModificationType.Renamed)
        {
            NewFileName = newFileName;
        }

        public string NewFileName { get; }
    }

    public class OpenFileResponse : ModifiedFileResponse
    {
        public OpenFileResponse(string fileName) : base(fileName, FileModificationType.Opened)
        {
        }
    }
}
