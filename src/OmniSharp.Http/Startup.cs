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
using OmniSharp.Eventing;
using OmniSharp.FileWatching;
using OmniSharp.Host.Internal;
using OmniSharp.Mef;
using OmniSharp.Middleware;
using OmniSharp.Options;
using OmniSharp.Roslyn;
using OmniSharp.Roslyn.Options;
using OmniSharp.Services;
using OmniSharp.Stdio.Logging;
using OmniSharp.Stdio.Services;
using OmniSharp.Utilities;

namespace OmniSharp
{
    public class Startup
    {
        private readonly IOmniSharpEnvironment _env;
        public IConfiguration Configuration { get; }
        public OmniSharpWorkspace Workspace { get; private set; }
        public CompositionHost PluginHost { get; private set; }

        public Startup(IOmniSharpEnvironment env, IConfiguration configuration)
        {
            _env = env;
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var mefBuilder = new OmniSharpMefBuilder();
        }

        public static CompositionHost CreateCompositionHost(IServiceProvider serviceProvider, OmniSharpOptions options, IEnumerable<Assembly> assemblies)
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

        public static void InitializeWorkspace(OmniSharpWorkspace workspace, CompositionHost compositionHost, IConfiguration configuration, ILogger logger, OmniSharpOptions options)
        {
            var projectEventForwarder = compositionHost.GetExport<ProjectEventForwarder>();
            projectEventForwarder.Initialize();

            // Initialize all the project systems
            foreach (var projectSystem in compositionHost.GetExports<IProjectSystem>())
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

            ProvideWorkspaceOptions(workspace, compositionHost, logger, options);

            // Mark the workspace as initialized
            workspace.Initialized = true;
        }

        private static void ProvideWorkspaceOptions(OmniSharpWorkspace workspace, CompositionHost compositionHost, ILogger logger, OmniSharpOptions options)
        {
            // run all workspace options providers
            foreach (var workspaceOptionsProvider in compositionHost.GetExports<IWorkspaceOptionsProvider>())
            {
                var providerName = workspaceOptionsProvider.GetType().FullName;

                try
                {
                    logger.LogInformation($"Invoking Workspace Options Provider: {providerName}");
                    workspace.Options = workspaceOptionsProvider.Process(workspace.Options, options.FormattingOptions);
                }
                catch (Exception e)
                {
                    var message = $"The workspace options provider '{providerName}' threw exception during initialization.";
                    logger.LogError(e, message);
                }
            }
        }

        public void Configure(
            IApplicationBuilder app,
            IServiceProvider serviceProvider,
            ILoggerFactory loggerFactory,
            ISharedTextWriter writer,
            IAssemblyLoader loader,
            IOptionsMonitor<OmniSharpOptions> options)
        {
            if (_env.TransportType == TransportType.Stdio)
            {
                loggerFactory.AddStdio(writer, (category, level) => LogFilter(category, level, _env));
            }
            else
            {
                loggerFactory.AddConsole((category, level) => LogFilter(category, level, _env));
            }

            var logger = loggerFactory.CreateLogger<Startup>();
            var assemblies = DiscoverOmniSharpAssemblies(loader, logger);

            PluginHost = CreateCompositionHost(serviceProvider, options.CurrentValue, assemblies);
            Workspace = PluginHost.GetExport<OmniSharpWorkspace>();

            app.UseRequestLogging();
            app.UseExceptionHandler("/error");
            app.UseMiddleware<EndpointMiddleware>();
            app.UseMiddleware<StatusMiddleware>();
            app.UseMiddleware<StopServerMiddleware>();

            if (_env.TransportType == TransportType.Stdio)
            {
                logger.LogInformation($"Omnisharp server running using {nameof(TransportType.Stdio)} at location '{_env.TargetDirectory}' on host {_env.HostProcessId}.");
            }
            else
            {
                logger.LogInformation($"Omnisharp server running on port '{_env.Port}' at location '{_env.TargetDirectory}' on host {_env.HostProcessId}.");
            }

            InitializeWorkspace(Workspace, PluginHost, Configuration, logger, options.CurrentValue);

            // when configuration options change
            // run workspace options providers automatically
            options.OnChange(o =>
            {
                ProvideWorkspaceOptions(Workspace, PluginHost, logger, o);
            });

            logger.LogInformation("Configuration finished.");
        }

        private static bool LogFilter(string category, LogLevel level, IOmniSharpEnvironment environment)
        {
            if (environment.LogLevel > level)
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
