using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Diagnostics;
using Microsoft.AspNet.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.OptionsModel;
using Microsoft.Extensions.PlatformAbstractions;
using OmniSharp.Mef;
using OmniSharp.Middleware;
using OmniSharp.Options;
using OmniSharp.Roslyn;
using OmniSharp.Services;
using OmniSharp.Stdio.Logging;
using OmniSharp.Stdio.Services;

namespace OmniSharp
{
    public class Startup
    {
        public Startup(IApplicationEnvironment applicationEnvironment)
        {
            var configurationBuilder = new ConfigurationBuilder()
                 .SetBasePath(applicationEnvironment.ApplicationBasePath)
                 .AddJsonFile("config.json")
                 .AddEnvironmentVariables();

            if (Program.Environment.OtherArgs != null)
            {
                configurationBuilder.AddCommandLine(Program.Environment.OtherArgs);
            }

            // Use the local omnisharp config if there's any in the root path
            if (File.Exists(Program.Environment.ConfigurationPath))
            {
                configurationBuilder.AddJsonFile(Program.Environment.ConfigurationPath);
            }

            Configuration = configurationBuilder.Build();
        }

        public IConfiguration Configuration { get; private set; }

        public OmnisharpWorkspace Workspace { get; set; }

        public CompositionHost PluginHost { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Add the omnisharp workspace to the container
            services.AddSingleton(typeof(OmnisharpWorkspace), (x) => Workspace);
            services.AddSingleton(typeof(CompositionHost), (x) => PluginHost);
            // Caching
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddOptions();
            // Setup the options from configuration
            services.Configure<OmniSharpOptions>(Configuration);
        }

        public static CompositionHost ConfigureMef(IServiceProvider serviceProvider,
                                                   OmniSharpOptions options,
                                                   IEnumerable<Assembly> assemblies,
                                                   ILogger logger,
                                                   Func<ContainerConfiguration, ContainerConfiguration> configurationAction)
        {
            var log = logger ?? serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<Startup>();

            log.LogInformation("Configuring MEF");

            var containerConfiguration = new ContainerConfiguration();
            assemblies = IncludeAssembliesFromType(assemblies, typeof(OmnisharpWorkspace), typeof(IRequest));

            foreach (var assembly in assemblies)
            {
                log.LogInformation($"with assembly {assembly.FullName}");
                containerConfiguration = containerConfiguration.WithAssembly(assembly);
            }

            var memoryCache = serviceProvider.GetService<IMemoryCache>();
            var env = serviceProvider.GetService<IOmnisharpEnvironment>();
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var writer = serviceProvider.GetService<ISharedTextWriter>();
            var applicationLifetime = serviceProvider.GetService<IApplicationLifetime>();

            containerConfiguration = containerConfiguration
                .WithProvider(MefValueProvider.From(serviceProvider))
                .WithProvider(MefValueProvider.From<IFileSystemWatcher>(new ManualFileSystemWatcher()))
                .WithProvider(MefValueProvider.From(memoryCache))
                .WithProvider(MefValueProvider.From(loggerFactory))
                .WithProvider(MefValueProvider.From(env))
                .WithProvider(MefValueProvider.From(writer))
                .WithProvider(MefValueProvider.From(applicationLifetime))
                .WithProvider(MefValueProvider.From(options))
                .WithProvider(MefValueProvider.From(options?.FormattingOptions ?? new FormattingOptions()));

            if (env.TransportType == TransportType.Stdio)
            {
                containerConfiguration = containerConfiguration
                    .WithProvider(MefValueProvider.From<IEventEmitter>(
                        new StdioEventEmitter(writer, loggerFactory)));
            }
            else
            {
                containerConfiguration = containerConfiguration
                    .WithProvider(MefValueProvider.From<IEventEmitter>(new NullEventEmitter()));
            }

            if (configurationAction != null)
            {
                containerConfiguration = configurationAction(containerConfiguration);
            }

            var container = containerConfiguration.CreateContainer();
            return container;
        }

        public void Configure(IApplicationBuilder app,
                              IServiceProvider serviceProvider,
                              ILibraryManager manager,
                              IOmnisharpEnvironment env,
                              ILoggerFactory loggerFactory,
                              ISharedTextWriter writer,
                              IOptions<OmniSharpOptions> optionsAccessor)
        {
            var logger = loggerFactory.CreateLogger<Startup>();

            if (env.TransportType == TransportType.Stdio)
            {
                loggerFactory.AddStdio(writer, (category, level) => LogFilter(category, level, env));
            }
            else
            {
                loggerFactory.AddConsole((category, level) => LogFilter(category, level, env));
            }

            var assemblies = manager.GetReferencingLibraries("OmniSharp.Abstractions")
                .SelectMany(libraryInformation => libraryInformation.Assemblies)
                .Concat(
                    manager.GetReferencingLibraries("OmniSharp.Roslyn")
                        .SelectMany(libraryInformation => libraryInformation.Assemblies)
                )
                .Select(assemblyName => Assembly.Load(assemblyName));

            PluginHost = ConfigureMef(serviceProvider, optionsAccessor.Value, assemblies, logger, configurationAction: null);

            Workspace = PluginHost.GetExport<OmnisharpWorkspace>();

            app.UseRequestLogging();
            app.UseExceptionHandler("/error");
            app.UseMiddleware<EndpointMiddleware>();
            app.UseMiddleware<StatusMiddleware>();
            app.UseMiddleware<StopServerMiddleware>();

            if (env.TransportType == TransportType.Stdio)
            {
                logger.LogInformation($"Omnisharp server running using stdio at location '{env.Path}' on host {env.HostPID}.");
            }
            else
            {
                logger.LogInformation($"Omnisharp server running on port '{env.Port}' at location '{env.Path}' on host {env.HostPID}.");
            }

            // Forward workspace events
            PluginHost.GetExport<ProjectEventForwarder>();

            logger.LogInformation($"Begining initializing project systems ...");
            foreach (var projectSystem in PluginHost.GetExports<IProjectSystem>())
            {
                try
                {
                    logger.LogInformation($"Initializing project system {projectSystem.Key}.");
                    projectSystem.Initalize(Configuration.GetSection(projectSystem.Key));
                }
                catch (Exception e)
                {
                    //if a project system throws an unhandled exception
                    //it should not crash the entire server
                    logger.LogError($"The project system '{projectSystem.GetType().Name}' threw an exception.", e);
                }
            }

            // Mark the workspace as initialized
            Workspace.Initialized = true;

            logger.LogInformation("Solution has finished loading");
        }

        private static IEnumerable<Assembly> IncludeAssembliesFromType(IEnumerable<Assembly> assemblies, params Type[] types)
        {
            return assemblies.Concat(types.Select(t => t.GetTypeInfo().Assembly))
                             .Distinct();
        }

        private static bool LogFilter(string category, LogLevel level, IOmnisharpEnvironment environment)
        {
            if (environment.TraceType > level)
            {
                return false;
            }

            if (string.Equals(category,
                              typeof(ExceptionHandlerMiddleware).FullName,
                              StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!category.StartsWith("OmniSharp", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(category,
                              typeof(WorkspaceInformationService).FullName,
                              StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(category,
                              typeof(ProjectEventForwarder).FullName,
                              StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
    }
}
