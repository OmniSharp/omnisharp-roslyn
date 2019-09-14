using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace OmniSharp.Models
{
    internal class ZeroBasedIndexConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(int) || objectType == typeof(int?) || objectType == typeof(IEnumerable<int>) || objectType == typeof(int[]);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (Configuration.ZeroBasedIndices)
            {
                return serializer.Deserialize(reader, objectType);
            }

            if (objectType == typeof(int[]))
            {
                var results = serializer.Deserialize<int[]>(reader);
                for (var i = 0; i < results.Length; i++)
                {
                    results[i] = results[i] - 1;
                }
                return results;
            }

            if (objectType == typeof(IEnumerable<int>))
            {
                var results = serializer.Deserialize<IEnumerable<int>>(reader);
                return results.Select(x => x - 1);
            }

            var deserializedValue = serializer.Deserialize<int?>(reader);
            if (objectType == typeof(int?))
            {
                deserializedValue = deserializedValue.Value - 1;
                return deserializedValue;
            }

            if (deserializedValue.HasValue)
            {
                return deserializedValue.Value - 1;
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                serializer.Serialize(writer, null);
                return;
            }

            if (Configuration.ZeroBasedIndices)
            {
                serializer.Serialize(writer, value);
                return;
            }

            var objectType = value.GetType();
            if (objectType == typeof(int[]))
            {
                var results = (int[])value;
                for (var i = 0; i < results.Length; i++)
                {
                    results[i] = results[i] + 1;
                }
            }

            else if (objectType == typeof(IEnumerable<int>))
            {
                var results = (IEnumerable<int>)value;
                value = results.Select(x => x + 1);
            }

            else if (objectType == typeof(int?))
            {
                var nullable = (int?)value;
                if (nullable.HasValue)
                {
                    nullable = nullable.Value + 1;
                }
                value = nullable;
            }

            else if (objectType == typeof(int))
            {
                var intValue = (int)value;
                value = intValue + 1;
            }

            serializer.Serialize(writer, value);
        }
    }
}
