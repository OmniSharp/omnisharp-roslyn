using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using Microsoft.Framework.Logging;

namespace OmniSharp.Middleware
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public LoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.Create<LoggingMiddleware>();
        }

        public async Task Invoke(HttpContext context)
        {
            var stream = context.Response.Body;
            Stopwatch stopwatch = null;
            if (_logger.IsEnabled(TraceType.Verbose))
            {
                stopwatch = Stopwatch.StartNew();
                LogRequest(context);

                // TODO: Add the feature interface to disable this memory stream
                // when we add signalr
                context.Response.Body = new MemoryStream();
            }

            await _next(context);

            if (_logger.IsEnabled(TraceType.Verbose))
            {
                LogResponse(context);

                // Copy stuff to the real body
                await context.Response.Body.CopyToAsync(stream);

                stopwatch.Stop();
                _logger.WriteVerbose(context.Request.Path + " " + stopwatch.ElapsedMilliseconds + "ms");
            }
        }

        private void LogRequest(HttpContext context)
        {
            _logger.WriteVerbose("************ Request ************");
            _logger.WriteVerbose(string.Format("{0} - {1}", context.Request.Method, context.Request.Path));
            _logger.WriteVerbose("************ Headers ************");

            foreach (var headerGroup in context.Request.Headers)
            {
                foreach (var header in headerGroup.Value)
                {
                    _logger.WriteVerbose(string.Format("{0} - {1}", headerGroup.Key, header));
                }
            }

            _logger.WriteVerbose("************  Body ************");
            using (var reader = new StreamReader(context.Request.Body))
            {
                var content = reader.ReadToEnd();
                _logger.WriteVerbose(content);
            }
        }

        private void LogResponse(HttpContext context)
        {
            _logger.WriteVerbose("************  Response ************ ");

            var reader = new StreamReader(context.Response.Body);
            var content = reader.ReadToEnd();
            _logger.WriteVerbose(content);

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