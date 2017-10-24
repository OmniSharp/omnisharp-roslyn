using Newtonsoft.Json;
using System;

namespace OmniSharp.Models.FilesChanged
{
    class FileChangeTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(FileChangeType);
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            switch (reader.Value as string)
            {
                case "Change":
                    return FileChangeType.Change;
                case "Create":
                    return FileChangeType.Create;
                case "Delete":
                    return FileChangeType.Delete;
                default:
                    return null;
            }
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotImplementedException();
    }
}
