using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Middleware
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public LoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<LoggingMiddleware>();
        }

        public async Task Invoke(HttpContext context)
        {
            var responseBody = context.Response.Body;
            var requestBody = context.Request.Body;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                // TODO: Add the feature interface to disable this memory stream
                // when we add signalr
                context.Response.Body = new MemoryStream();
                context.Request.Body = new MemoryStream();

                await requestBody.CopyToAsync(context.Request.Body);

                LogRequest(context);

            }

            var stopwatch = Stopwatch.StartNew();
            await _next(context);
            stopwatch.Stop();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogResponse(context);

                await context.Response.Body.CopyToAsync(responseBody);

            }
            _logger.LogInformation(context.Request.Path + ": " + context.Response.StatusCode + " " + stopwatch.ElapsedMilliseconds + "ms");
        }

        private void LogRequest(HttpContext context)
        {
            _logger.LogDebug("************ Request ************");
            _logger.LogDebug(string.Format("{0} - {1}", context.Request.Method, context.Request.Path));
            _logger.LogDebug("************ Headers ************");

            foreach (var headerGroup in context.Request.Headers)
            {
                foreach (var header in headerGroup.Value)
                {
                    _logger.LogDebug(string.Format("{0} - {1}", headerGroup.Key, header));
                }
            }

            context.Request.Body.Position = 0;

            _logger.LogDebug("************  Body ************");
            var reader = new StreamReader(context.Request.Body);
            var content = reader.ReadToEnd();
            _logger.LogDebug(content);

            context.Request.Body.Position = 0;
        }

        private void LogResponse(HttpContext context)
        {
            _logger.LogDebug("************  Response ************ ");

            context.Response.Body.Position = 0;

            var reader = new StreamReader(context.Response.Body);
            var content = reader.ReadToEnd();
            _logger.LogDebug(content);
            context.Response.Body.Position = 0;
        }
    }

    public static class LoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
        {
            return app.UseMiddleware<LoggingMiddleware>();
        }
    }
}