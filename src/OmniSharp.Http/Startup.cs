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
        private CompositionHost _compositionHost;

        public static void AddOmniSharpServices(
            IServiceCollection services,
            IOmniSharpEnvironment environment,
            IEventEmitter eventEmitter,
            ConfigurationResult configurationResult)
        {
            CompositionHostBuilder.ConfigureDefaultServices(environment, configurationResult.Configuration, eventEmitter, services,
                configureLogging: builder =>
                {
                    builder.AddConsole();

                    var workspaceInformationServiceName = typeof(WorkspaceInformationService).FullName;
                    var projectEventForwarder = typeof(ProjectEventForwarder).FullName;
                    var exceptionHandlerMiddlewareName = typeof(ExceptionHandlerMiddleware).FullName;

                    builder.AddFilter(
                        (category, logLevel) =>
                            category.Equals(exceptionHandlerMiddlewareName, StringComparison.OrdinalIgnoreCase) ||
                            (environment.LogLevel <= logLevel &&
                                category.StartsWith("OmniSharp", StringComparison.OrdinalIgnoreCase) &&
                                !category.Equals(workspaceInformationServiceName, StringComparison.OrdinalIgnoreCase) &&
                                !category.Equals(projectEventForwarder, StringComparison.OrdinalIgnoreCase)));
                });
        }

        public void Configure(
            IApplicationBuilder app,
            IServiceProvider serviceProvider,
            ILoggerFactory loggerFactory,
            HttpEnvironment httpEnvironment,
            IOmniSharpEnvironment environment,
            PluginAssemblies commandLinePlugins,
            ConfigurationResult configurationResult)
        {
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<OmniSharpOptions>>();
            var plugins = commandLinePlugins.AssemblyNames.Concat(options.CurrentValue.Plugins.GetNormalizedLocationPaths(environment));

            var logger = loggerFactory.CreateLogger<Startup>();

            if (configurationResult.HasError())
            {
                logger.LogError(configurationResult.Exception, "There was an error when reading the OmniSharp configuration, starting with the default options.");
            }

            var assemblyLoader = serviceProvider.GetRequiredService<IAssemblyLoader>();
            _compositionHost = new CompositionHostBuilder(serviceProvider)
                .WithOmniSharpAssemblies()
                .WithAssemblies(assemblyLoader.LoadByAssemblyNameOrPath(logger, plugins).ToArray())
                .Build(environment.TargetDirectory);

            var workspace = _compositionHost.GetExport<OmniSharpWorkspace>();

            logger.LogInformation($"Starting OmniSharp on {Platform.Current}");

            app.UseMiddleware<LoggingMiddleware>();
            app.UseExceptionHandler("/error");
            app.UseMiddleware<EndpointMiddleware>(_compositionHost);
            app.UseMiddleware<StatusMiddleware>(workspace);
            app.UseMiddleware<StopServerMiddleware>();

            WorkspaceInitializer.Initialize(serviceProvider, _compositionHost);

            logger.LogInformation($"Omnisharp server running on port '{httpEnvironment.Port}' at location '{environment.TargetDirectory}' on host {environment.HostProcessId}.");
        }
    }
}
