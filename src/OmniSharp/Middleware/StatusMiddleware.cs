using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using Newtonsoft.Json;

namespace OmniSharp
{
    public class StatusMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly OmnisharpWorkspace _workspace;

        public StatusMiddleware(RequestDelegate next, OmnisharpWorkspace workspace)
        {
            _next = next;
            _workspace = workspace;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path.HasValue)
            {
                var endpoint = httpContext.Request.Path.Value;
                if (endpoint == OmnisharpEndpoints.CheckAliveStatus)
                {
                    SerializeResponseObject(httpContext.Response, true);
                    return;
                }

                if (endpoint == OmnisharpEndpoints.CheckReadyStatus)
                {
                    SerializeResponseObject(httpContext.Response, _workspace.Initialized);
                    return;
                }
            }

            await _next(httpContext);
        }

        private void SerializeResponseObject(HttpResponse response, object value)
        {
            using (var writer = new StreamWriter(response.Body))
            {
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    jsonWriter.CloseOutput = false;
                    var jsonSerializer = JsonSerializer.Create(/*TODO: SerializerSettings*/);
                    jsonSerializer.Serialize(jsonWriter, value);
                }
            }
        }
    }
}
