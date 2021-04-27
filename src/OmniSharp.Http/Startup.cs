using System;
using System.Composition.Hosting;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Eventing;
using OmniSharp.Http.Middleware;
using OmniSharp.Options;
using OmniSharp.Plugins;
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
        private PluginAssemblies _commandLinePlugins;

        public Startup(IOmniSharpEnvironment environment, IEventEmitter eventEmitter, PluginAssemblies commandLinePlugins)
        {
            _environment = environment;
            _eventEmitter = eventEmitter;
            _commandLinePlugins = commandLinePlugins;
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            var configurationResult = new ConfigurationBuilder(_environment).Build();
            var serviceProvider = CompositionHostBuilder.CreateDefaultServiceProvider(_environment, configurationResult.Configuration, _eventEmitter, services,
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

            var options = serviceProvider.GetRequiredService<IOptionsMonitor<OmniSharpOptions>>();
            var plugins = _commandLinePlugins.AssemblyNames.Concat(options.CurrentValue.Plugins.GetNormalizedLocationPaths(_environment));

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Startup>();

            if (configurationResult.HasError())
            {
                logger.LogError(configurationResult.Exception, "There was an error when reading the OmniSharp configuration, starting with the default options.");
            }

            var assemblyLoader = serviceProvider.GetRequiredService<IAssemblyLoader>();
            _compositionHost = new CompositionHostBuilder(serviceProvider)
                .WithOmniSharpAssemblies()
                .WithAssemblies(assemblyLoader.LoadByAssemblyNameOrPath(logger, plugins).ToArray())
                .Build(_environment.TargetDirectory);

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
