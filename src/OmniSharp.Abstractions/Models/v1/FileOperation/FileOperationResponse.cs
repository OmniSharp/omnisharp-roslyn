using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OmniSharp.Models
{
    public abstract class FileOperationResponse
    {
        public FileOperationResponse(string fileName, FileModificationType type)
        {
            FileName = fileName;
            ModificationType = type;
        }

        public string FileName { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public FileModificationType ModificationType { get; }
    }
}
