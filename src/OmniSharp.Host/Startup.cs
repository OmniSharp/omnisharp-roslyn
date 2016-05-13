using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        public Startup()
        {
            var appEnv = PlatformServices.Default.Application;
            
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(appEnv.ApplicationBasePath)
                .AddJsonFile("config.json", optional: true)
                .AddEnvironmentVariables();

            if (Program.Environment.OtherArgs != null)
            {
                configBuilder.AddCommandLine(Program.Environment.OtherArgs);
            }

            // Use the local omnisharp config if there's any in the root path
            configBuilder.AddJsonFile(source =>
            {
                source.Path = "omnisharp.json";
                source.Optional = true;
                source.FileProvider = new PhysicalFileProvider(Program.Environment.Path);
            });

            Configuration = configBuilder.Build();
        }

        public IConfiguration Configuration { get; }

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
            var loader = serviceProvider.GetService<IOmnisharpAssemblyLoader>();

            config = config
                .WithProvider(MefValueProvider.From(serviceProvider))
                .WithProvider(MefValueProvider.From<IFileSystemWatcher>(new ManualFileSystemWatcher()))
                .WithProvider(MefValueProvider.From(memoryCache))
                .WithProvider(MefValueProvider.From(loggerFactory))
                .WithProvider(MefValueProvider.From(env))
                .WithProvider(MefValueProvider.From(writer))
                .WithProvider(MefValueProvider.From(applicationLifetime))
                .WithProvider(MefValueProvider.From(options))
                .WithProvider(MefValueProvider.From(options.FormattingOptions))
                .WithProvider(MefValueProvider.From(loader))
                .WithProvider(MefValueProvider.From(new MetadataHelper(loader))); // other way to do singleton and autowire?

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

        public void Configure(IApplicationBuilder app,
                              IServiceProvider serviceProvider,
                              IOmnisharpEnvironment env,
                              ILoggerFactory loggerFactory,
                              ISharedTextWriter writer,
                              IOmnisharpAssemblyLoader loader,
                              IOptions<OmniSharpOptions> optionsAccessor)
        {
            Func<RuntimeLibrary, bool> shouldLoad = lib => lib.Dependencies.Any(dep => dep.Name == "OmniSharp.Abstractions" ||
                                                                                       dep.Name == "OmniSharp.Roslyn");

            var dependencyContext = DependencyContext.Default;
            var assemblies = dependencyContext.RuntimeLibraries
                                              .Where(shouldLoad)
                                              .SelectMany(lib => lib.GetDefaultAssemblyNames(dependencyContext))
                                              .Select(each => loader.Load(each.Name))
                                              .ToList();

            PluginHost = ConfigureMef(serviceProvider, optionsAccessor.Value, assemblies);

            Workspace = PluginHost.GetExport<OmnisharpWorkspace>();

            if (env.TransportType == TransportType.Stdio)
            {
                loggerFactory.AddStdio(writer, (category, level) => LogFilter(category, level, env));
            }
            else
            {
                loggerFactory.AddConsole((category, level) => LogFilter(category, level, env));
            }

            var logger = loggerFactory.CreateLogger<Startup>();

            foreach (var assembly in assemblies)
            {
                logger.LogDebug($"Loaded {assembly.FullName}");
            }

            app.UseRequestLogging();
            app.UseExceptionHandler("/error");
            app.UseMiddleware<EndpointMiddleware>();
            app.UseMiddleware<StatusMiddleware>();
            app.UseMiddleware<StopServerMiddleware>();

            if (env.TransportType == TransportType.Stdio)
            {
                logger.LogInformation($"Omnisharp server running using {nameof(TransportType.Stdio)} at location '{env.Path}' on host {env.HostPID}.");
            }
            else
            {
                logger.LogInformation($"Omnisharp server running on port '{env.Port}' at location '{env.Path}' on host {env.HostPID}.");
            }

            // ProjectEventForwarder register event to OmnisharpWorkspace during instantiation
            PluginHost.GetExport<ProjectEventForwarder>();

            // Initialize all the project systems
            foreach (var projectSystem in PluginHost.GetExports<IProjectSystem>())
            {
                try
                {
                    projectSystem.Initalize(Configuration.GetSection(projectSystem.Key));
                }
                catch (Exception e)
                {
                    var message = $"The project system '{projectSystem.GetType().Name}' threw exception during initialization.\n{e.Message}\n{e.StackTrace}";
                    // if a project system throws an unhandled exception it should not crash the entire server
                    logger.LogError(message);
                }
            }

            // Mark the workspace as initialized
            Workspace.Initialized = true;

            logger.LogInformation("Configuration finished.");
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
