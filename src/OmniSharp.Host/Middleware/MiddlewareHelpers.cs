using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace OmniSharp.Middleware
{
    public static class MiddlewareHelpers
    {
        private static readonly Encoding _encoding = new System.Text.UTF8Encoding(false);

        public static void WriteTo(HttpResponse response, object value)
        {
            using (var writer = new StreamWriter(response.Body, _encoding, 1024, true))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                jsonWriter.CloseOutput = false;
                var jsonSerializer = JsonSerializer.Create(/*TODO: SerializerSettings*/);
                jsonSerializer.Serialize(jsonWriter, value);
            }
        }
    }
}