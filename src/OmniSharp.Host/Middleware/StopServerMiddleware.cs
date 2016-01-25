using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace OmniSharp
{
    public class StopServerMiddleware
    {
        private readonly IApplicationLifetime _lifetime;

        public StopServerMiddleware(RequestDelegate next, IApplicationLifetime lifetime)
        {
            _lifetime = lifetime;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path.HasValue)
            {
                var endpoint = httpContext.Request.Path.Value;
                if (endpoint == OmnisharpEndpoints.StopServer)
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
