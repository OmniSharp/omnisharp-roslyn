using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace OmniSharp.Http.Middleware
{
    static class MiddlewareExtensions
    {
        private static readonly JsonSerializer _jsonSerializer = JsonSerializer.Create();
        private static readonly Encoding _encoding = new UTF8Encoding(false);

        public static void WriteJson(this HttpResponse response, object value)
        {
            using (var writer = new StreamWriter(response.Body, _encoding, 1024, true))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                jsonWriter.CloseOutput = false;
                _jsonSerializer.Serialize(jsonWriter, value);
            }
        }
    }
}
