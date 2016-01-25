using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Mef;
using OmniSharp.Middleware.Endpoint;
using OmniSharp.Models;
using OmniSharp.Plugins;
using OmniSharp.Services;

namespace OmniSharp.Middleware
{
    public class EndpointMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HashSet<string> _endpoints;
        private readonly IReadOnlyDictionary<string, Lazy<EndpointHandler>> _endpointHandlers;
        private readonly CompositionHost _host;
        private readonly ILogger _logger;
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        public EndpointMiddleware(RequestDelegate next, CompositionHost host, ILoggerFactory loggerFactory)
        {
            _next = next;
            _host = host;
            _projectSystems = host.GetExports<IProjectSystem>();
            _logger = loggerFactory.CreateLogger<EndpointMiddleware>();
            var endpoints = _host.GetExports<Lazy<IRequest, EndpointDescriptor>>()
                .Select(x => x.Metadata);

            var handlers = _host.GetExports<Lazy<IRequestHandler, OmniSharpLanguage>>();

            _endpoints = new HashSet<string>(
                    endpoints
                        .Select(x => x.EndpointName)
                        .Distinct(),
                    StringComparer.OrdinalIgnoreCase
                );

            var updateBufferEndpointHandler = new Lazy<EndpointHandler<UpdateBufferRequest, object>>(() => (EndpointHandler<UpdateBufferRequest, object>)_endpointHandlers[OmnisharpEndpoints.UpdateBuffer].Value);
            var languagePredicateHandler = new LanguagePredicateHandler(_projectSystems);
            var projectSystemPredicateHandler = new StaticLanguagePredicateHandler("Projects");
            var nugetPredicateHandler = new StaticLanguagePredicateHandler("NuGet");
            var endpointHandlers = endpoints.ToDictionary(
                    x => x.EndpointName,
                    endpoint => new Lazy<EndpointHandler>(() =>
                    {
                        IPredicateHandler handler;

                        // Projects are a special case, this allows us to select the correct "Projects" language for them
                        if (endpoint.EndpointName == OmnisharpEndpoints.ProjectInformation || endpoint.EndpointName == OmnisharpEndpoints.WorkspaceInformation)
                            handler = projectSystemPredicateHandler;
                        else if (endpoint.EndpointName == OmnisharpEndpoints.PackageSearch || endpoint.EndpointName == OmnisharpEndpoints.PackageSource || endpoint.EndpointName == OmnisharpEndpoints.PackageVersion)
                            handler = nugetPredicateHandler;
                        else
                            handler = languagePredicateHandler;

                        // This lets any endpoint, that contains a Request object, invoke update buffer.
                        // The language will be same language as the caller, this means any language service
                        // must implement update buffer.
                        var updateEndpointHandler = updateBufferEndpointHandler;
                        if (endpoint.EndpointName == OmnisharpEndpoints.UpdateBuffer)
                        {
                            // We don't want to call update buffer on update buffer.
                            updateEndpointHandler = new Lazy<EndpointHandler<UpdateBufferRequest, object>>(() => null);
                        }

                        return EndpointHandler.Factory(handler, _host, _logger, endpoint, handlers, updateEndpointHandler, Enumerable.Empty<Plugin>());
                    }),
                    StringComparer.OrdinalIgnoreCase
                );

            _endpointHandlers = new ReadOnlyDictionary<string, Lazy<EndpointHandler>>(endpointHandlers);
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path.HasValue)
            {
                var endpoint = httpContext.Request.Path.Value;
                if (_endpoints.Contains(endpoint))
                {
                    Lazy<EndpointHandler> handler;
                    if (_endpointHandlers.TryGetValue(endpoint, out handler))
                    {
                        var response = await handler.Value.Handle(httpContext);
                        MiddlewareHelpers.WriteTo(httpContext.Response, response);
                        return;
                    }
                }
            }

            await _next(httpContext);
        }
    }

    public static class EndpointMiddlewareExtensions
    {
        public static IApplicationBuilder UseEndpointMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<EndpointMiddleware>();
        }
    }
}
