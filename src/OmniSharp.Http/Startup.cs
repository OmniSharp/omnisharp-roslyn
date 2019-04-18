using System;
using System.Composition.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.Http.Middleware;
using OmniSharp.Roslyn;
using OmniSharp.Services;
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
            var serviceProvider = CompositionHostBuilder.CreateDefaultServiceProvider(_environment, configuration, _eventEmitter, services,
                configureLogging: builder =>
                {
                    builder.AddConsole();

                    var workspaceInformationServiceName = typeof(WorkspaceInformationService).FullName;
                    var projectEventForwarder = typeof(ProjectEventForwarder).FullName;
                    var exceptionHandlerMiddlewareName = typeof(ExceptionHandlerMiddleware).FullName;

                    builder.AddFilter(
                        (category, logLevel) =>
                            category.Equals(exceptionHandlerMiddlewareName, StringComparison.OrdinalIgnoreCase) ||
                            (_environment.LogLevel <= logLevel &&
                                category.StartsWith("OmniSharp", StringComparison.OrdinalIgnoreCase) &&
                                !category.Equals(workspaceInformationServiceName, StringComparison.OrdinalIgnoreCase) &&
                                !category.Equals(projectEventForwarder, StringComparison.OrdinalIgnoreCase)));
                });

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
