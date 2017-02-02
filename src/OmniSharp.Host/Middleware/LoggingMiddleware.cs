using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OmniSharp.Utilities;

namespace OmniSharp.Middleware
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly CachedStringBuilder _cachedBuilder;

        public LoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<LoggingMiddleware>();
            _cachedBuilder = new CachedStringBuilder();
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

            _logger.LogInformation($"{context.Request.Path}: {context.Response.StatusCode} {stopwatch.ElapsedMilliseconds}ms");
        }

        private void LogRequest(HttpContext context)
        {
            var builder = _cachedBuilder.Acquire();
            try
            {
                builder.AppendLine("************ Request ************");
                builder.AppendLine($"{context.Request.Method} - {context.Request.Path}");
                builder.AppendLine("************ Headers ************");

                foreach (var headerGroup in context.Request.Headers)
                {
                    foreach (var header in headerGroup.Value)
                    {
                        builder.AppendLine($"{headerGroup.Key} - {header}");
                    }
                }

                context.Request.Body.Position = 0;

                builder.AppendLine("************  Body ************");

                var reader = new StreamReader(context.Request.Body);
                var content = reader.ReadToEnd();

                builder.Append(content);
                _logger.LogDebug(builder.ToString());

                context.Request.Body.Position = 0;
            }
            finally
            {
                _cachedBuilder.Release(builder);
            }
        }

        private void LogResponse(HttpContext context)
        {
            var builder = _cachedBuilder.Acquire();
            try
            {
                builder.AppendLine("************  Response ************ ");

                context.Response.Body.Position = 0;

                var reader = new StreamReader(context.Response.Body);
                var content = reader.ReadToEnd();

                builder.Append(content);
                _logger.LogDebug(builder.ToString());

                context.Response.Body.Position = 0;
            }
            finally
            {
                _cachedBuilder.Release(builder);
            }
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