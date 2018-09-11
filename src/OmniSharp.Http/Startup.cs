using System;
using System.Composition.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.Http.Middleware;
using OmniSharp.Stdio.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Http
{
    internal class Startup
    {
        private readonly IOmniSharpEnvironment _environment;
        private readonly IEventEmitter _eventEmitter;
        private CompositionHost _compositionHost;

        public Startup(IOmniSharpEnvironment environment, IEventEmitter eventEmitter, ISharedTextWriter writer)
        {
            _environment = environment;
            _eventEmitter = eventEmitter;
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            var configuration = new ConfigurationBuilder(_environment).Build();
            var serviceProvider = CompositionHostBuilder.CreateDefaultServiceProvider(_environment, configuration, _eventEmitter, services);

            _compositionHost = new CompositionHostBuilder(serviceProvider)
                .WithOmniSharpAssemblies()
                .Build();

            return serviceProvider;
        }

        public void Configure(
            IApplicationBuilder app,
            IServiceProvider serviceProvider,
            ILoggerFactory loggerFactory,
            HttpEnvironment httpEnvironment)
        {
            var workspace = _compositionHost.GetExport<OmniSharpWorkspace>();
            var logger = loggerFactory.CreateLogger<Startup>();

            loggerFactory.AddConsole((category, level) =>
            {
                if (HostHelpers.LogFilter(category, level, _environment)) return true;

                if (string.Equals(category, typeof(ExceptionHandlerMiddleware).FullName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            });

            logger.LogInformation($"Starting OmniSharp on {Platform.Current}");

            app.UseRequestLogging();
            app.UseExceptionHandler("/error");
            app.UseMiddleware<EndpointMiddleware>(_compositionHost);
            app.UseMiddleware<StatusMiddleware>(workspace);
            app.UseMiddleware<StopServerMiddleware>();

            WorkspaceInitializer.Initialize(serviceProvider, _compositionHost);

            logger.LogInformation($"Omnisharp server running on port '{httpEnvironment.Port}' at location '{_environment.TargetDirectory}' on host {_environment.HostProcessId}.");
        }
    }
}
