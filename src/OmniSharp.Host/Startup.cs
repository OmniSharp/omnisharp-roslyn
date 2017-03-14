using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Mef;
using OmniSharp.Middleware;
using OmniSharp.Options;
using OmniSharp.Roslyn;
using OmniSharp.Services;
using OmniSharp.Services.FileWatching;
using OmniSharp.Stdio.Logging;
using OmniSharp.Stdio.Services;

namespace OmniSharp
{
    public class Startup
    {
        private readonly IOmniSharpEnvironment _env;
        public IConfiguration Configuration { get; }
        public OmniSharpWorkspace Workspace { get; private set; }
        public CompositionHost PluginHost { get; private set; }

        public Startup(IOmniSharpEnvironment env)
        {
            _env = env;

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("config.json", optional: true)
                .AddEnvironmentVariables();

            if (env.OtherArgs != null)
            {
                configBuilder.AddCommandLine(env.OtherArgs);
            }

            // Use the local omnisharp config if there's any in the root path
            configBuilder.AddJsonFile(
                new PhysicalFileProvider(env.Path),
                "omnisharp.json",
                optional: true,
                reloadOnChange: false);

            Configuration = configBuilder.Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Add the omnisharp workspace to the container
            services.AddSingleton(typeof(OmniSharpWorkspace), _ => Workspace);
            services.AddSingleton(typeof(CompositionHost), _ => PluginHost);

            // Caching
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddOptions();

            // Setup the options from configuration
            services.Configure<OmniSharpOptions>(Configuration);
        }

        public static CompositionHost CreateComposition(IServiceProvider serviceProvider, OmniSharpOptions options, IEnumerable<Assembly> assemblies)
        {
            var config = new ContainerConfiguration();
            assemblies = assemblies
                .Concat(new[] { typeof(OmniSharpWorkspace).GetTypeInfo().Assembly, typeof(IRequest).GetTypeInfo().Assembly })
                .Distinct();

            foreach (var assembly in assemblies)
            {
                config = config.WithAssembly(assembly);
            }

            var memoryCache = serviceProvider.GetService<IMemoryCache>();
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var env = serviceProvider.GetService<IOmniSharpEnvironment>();
            var writer = serviceProvider.GetService<ISharedTextWriter>();
            var loader = serviceProvider.GetService<IAssemblyLoader>();

            var fileSystemWatcher = new ManualFileSystemWatcher();
            var metadataHelper = new MetadataHelper(loader);

            config = config
                .WithProvider(MefValueProvider.From(serviceProvider))
                .WithProvider(MefValueProvider.From<IFileSystemWatcher>(fileSystemWatcher))
                .WithProvider(MefValueProvider.From(memoryCache))
                .WithProvider(MefValueProvider.From(loggerFactory))
                .WithProvider(MefValueProvider.From(env))
                .WithProvider(MefValueProvider.From(writer))
                .WithProvider(MefValueProvider.From(options))
                .WithProvider(MefValueProvider.From(options.FormattingOptions))
                .WithProvider(MefValueProvider.From(loader))
                .WithProvider(MefValueProvider.From(metadataHelper));

            if (env.TransportType == TransportType.Stdio)
            {
                config = config
                    .WithProvider(MefValueProvider.From<IEventEmitter>(new StdioEventEmitter(writer)));
            }
            else
            {
                config = config
                    .WithProvider(MefValueProvider.From(NullEventEmitter.Instance));
            }

            return config.CreateContainer();
        }

        private static void InitializeWorkspace(OmniSharpWorkspace workspace, CompositionHost composition, IConfiguration configuration, ILogger logger)
        {
            var projectEventForwarder = composition.GetExport<ProjectEventForwarder>();
            projectEventForwarder.Initialize();

            // Initialize all the project systems
            foreach (var projectSystem in composition.GetExports<IProjectSystem>())
            {
                try
                {
                    projectSystem.Initalize(configuration.GetSection(projectSystem.Key));
                }
                catch (Exception e)
                {
                    var message = $"The project system '{projectSystem.GetType().FullName}' threw exception during initialization.";
                    // if a project system throws an unhandled exception it should not crash the entire server
                    logger.LogError(e, message);
                }
            }

            // run all workspace options providers
            foreach (var workspaceOptionsProvider in composition.GetExports<IWorkspaceOptionsProvider>())
            {
                try
                {
                    workspace.Options = workspaceOptionsProvider.Process(workspace.Options);
                }
                catch (Exception e)
                {
                    var message = $"The workspace options provider '{workspaceOptionsProvider.GetType().FullName}' threw exception during initialization.";
                    logger.LogError(e, message);
                }
            }

            // Mark the workspace as initialized
            workspace.Initialized = true;
        }

        public void Configure(
            IApplicationBuilder app,
            IServiceProvider serviceProvider,
            ILoggerFactory loggerFactory,
            ISharedTextWriter writer,
            IAssemblyLoader loader,
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

            PluginHost = CreateComposition(serviceProvider, optionsAccessor.Value, assemblies);

            Workspace = PluginHost.GetExport<OmniSharpWorkspace>();

            if (_env.TransportType == TransportType.Stdio)
            {
                loggerFactory.AddStdio(writer, (category, level) => LogFilter(category, level, _env));
            }
            else
            {
                loggerFactory.AddConsole((category, level) => LogFilter(category, level, _env));
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

            if (_env.TransportType == TransportType.Stdio)
            {
                logger.LogInformation($"Omnisharp server running using {nameof(TransportType.Stdio)} at location '{_env.Path}' on host {_env.HostPID}.");
            }
            else
            {
                logger.LogInformation($"Omnisharp server running on port '{_env.Port}' at location '{_env.Path}' on host {_env.HostPID}.");
            }

            InitializeWorkspace(Workspace, PluginHost, Configuration, logger);

            logger.LogInformation("Configuration finished.");
        }

        private static bool LogFilter(string category, LogLevel level, IOmniSharpEnvironment environment)
        {
            if (environment.TraceType > level)
            {
                return false;
            }

            if (string.Equals(category, typeof(ExceptionHandlerMiddleware).FullName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!category.StartsWith("OmniSharp", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(category, typeof(WorkspaceInformationService).FullName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(category, typeof(ProjectEventForwarder).FullName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
    }
}
