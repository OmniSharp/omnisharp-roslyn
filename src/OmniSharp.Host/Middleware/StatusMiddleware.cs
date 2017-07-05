using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace OmniSharp.Middleware
{
    public class StatusMiddleware
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
                    MiddlewareHelpers.WriteTo(httpContext.Response, true);
                    return;
                }

                if (endpoint == OmniSharpEndpoints.CheckReadyStatus)
                {
                    MiddlewareHelpers.WriteTo(httpContext.Response, _workspace.Initialized);
                    return;
                }
            }

            await _next(httpContext);
        }
    }
}