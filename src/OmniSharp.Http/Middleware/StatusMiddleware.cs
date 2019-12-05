using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace OmniSharp.Http.Middleware
{
    class StatusMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly OmniSharpWorkspace _workspace;

        public StatusMiddleware(RequestDelegate next, OmniSharpWorkspace workspace)
        {
            _next = next;
            _workspace = workspace;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path.HasValue)
            {
                var endpoint = httpContext.Request.Path.Value;
                if (endpoint == OmniSharpEndpoints.CheckAliveStatus)
                {
                    httpContext.Response.WriteJson(true);
                    return;
                }

                if (endpoint == OmniSharpEndpoints.CheckReadyStatus)
                {
                    httpContext.Response.WriteJson(_workspace.Initialized);
                    return;
                }
            }

            await _next(httpContext);
        }
    }
}
