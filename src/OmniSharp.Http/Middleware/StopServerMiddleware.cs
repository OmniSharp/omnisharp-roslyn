using System.Threading;
using System.Threading.Tasks;
#if NETCOREAPP
using Microsoft.Extensions.Hosting;
#else
using Microsoft.AspNetCore.Hosting;
#endif
using Microsoft.AspNetCore.Http;

namespace OmniSharp.Http.Middleware
{
    class StopServerMiddleware
    {
#if NETCOREAPP
        private readonly IHostApplicationLifetime _lifetime;

        public StopServerMiddleware(RequestDelegate next, IHostApplicationLifetime lifetime)
#else
        private readonly IApplicationLifetime _lifetime;

        public StopServerMiddleware(RequestDelegate next, IApplicationLifetime lifetime)
#endif
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
