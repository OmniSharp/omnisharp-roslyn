using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Diagnostics;
using Microsoft.AspNet.Hosting;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Caching.Memory;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;
using OmniSharp.Dnx;
using OmniSharp.Mef;
using OmniSharp.Middleware;
using OmniSharp.Options;
using OmniSharp.Plugins;
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
            var configurationBuilder = new ConfigurationBuilder(applicationEnvironment.ApplicationBasePath)
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
                                                          Func<ContainerConfiguration, ContainerConfiguration> configure = null)
        {
            var config = new ContainerConfiguration();
            assemblies = assemblies
                .Concat(new[] { typeof(OmnisharpWorkspace).GetTypeInfo().Assembly, typeof(IRequest).GetTypeInfo().Assembly })
                .Distinct();

            foreach (var assembly in assemblies)
            {
                config = config.WithAssembly(assembly);
            }

            var memoryCache = serviceProvider.GetService<IMemoryCache>();
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var env = serviceProvider.GetService<IOmnisharpEnvironment>();
            var writer = serviceProvider.GetService<ISharedTextWriter>();
            var applicationLifetime = serviceProvider.GetService<IApplicationLifetime>();

            config = config
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
                config = config
                    .WithProvider(MefValueProvider.From<IEventEmitter>(new StdioEventEmitter(writer)));
            }
            else
            {
                config = config
                    .WithProvider(MefValueProvider.From<IEventEmitter>(new NullEventEmitter()));
            }

            if (configure != null)
                config = configure(config);

            var container = config.CreateContainer();
            return container;
        }

        public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider, ILibraryManager manager,
            IOmnisharpEnvironment env, ILoggerFactory loggerFactory, ISharedTextWriter writer, IOptions<OmniSharpOptions> optionsAccessor)
        {
            var assemblies = manager.GetReferencingLibraries("OmniSharp.Abstractions")
                .SelectMany(libraryInformation => libraryInformation.Assemblies)
                .Concat(
                    manager.GetReferencingLibraries("OmniSharp.Roslyn")
                        .SelectMany(libraryInformation => libraryInformation.Assemblies)
                )
                .Select(assemblyName => Assembly.Load(assemblyName));

            PluginHost = ConfigureMef(serviceProvider, optionsAccessor.Options, assemblies);

            Workspace = PluginHost.GetExport<OmnisharpWorkspace>();

            Func<string, LogLevel, bool> logFilter = (category, type) =>
                (category.StartsWith("OmniSharp", StringComparison.OrdinalIgnoreCase) || string.Equals(category, typeof(ErrorHandlerMiddleware).FullName, StringComparison.OrdinalIgnoreCase))
                && env.TraceType <= type;

            if (env.TransportType == TransportType.Stdio)
            {
                loggerFactory.AddStdio(writer, logFilter);
            }
            else
            {
                loggerFactory.AddConsole(logFilter);
            }

            var logger = loggerFactory.CreateLogger<Startup>();

            app.UseRequestLogging();

            app.UseErrorHandler("/error");

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
            foreach (var projectSystem in PluginHost.GetExports<IProjectSystem>())
            {
                try
                {
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
    }
}
