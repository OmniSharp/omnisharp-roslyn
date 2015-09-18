using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using Microsoft.Framework.Runtime;

namespace OmniSharp
{
    public class StopServerMiddleware
    {
        private readonly IApplicationShutdown _applicationShutdown;

        public StopServerMiddleware(RequestDelegate next, IApplicationShutdown applicationShutdown)
        {
            _applicationShutdown = applicationShutdown;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path.HasValue)
            {
                var endpoint = httpContext.Request.Path.Value;
                if (endpoint == "/stopserver")
                {
                    await Task.Run(() =>
                    {
                        Thread.Sleep(200);
                        _applicationShutdown.RequestShutdown();
                    });
                }
            }
        }
    }
}
