using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;

namespace OmniSharp.Http.Middleware
{
    class StopServerMiddleware
    {
        private readonly IHostApplicationLifetime _lifetime;

        public StopServerMiddleware(RequestDelegate next, IHostApplicationLifetime lifetime)
        {
            _lifetime = lifetime;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path.HasValue)
            {
                var endpoint = httpContext.Request.Path.Value;
                if (endpoint == OmniSharpEndpoints.StopServer)
                {
                    await Task.Run(() =>
                    {
                        Thread.Sleep(200);
                        _lifetime.StopApplication();
                    });
                }
            }
        }
    }
}
