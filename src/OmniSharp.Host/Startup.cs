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

        public Startup(IOmniSharpEnvironment env)
        {
            _env = env;

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(Constants.ConfigFile, optional: true)
                .AddEnvironmentVariables("OMNISHARP_");

            if (env.AdditionalArguments?.Length > 0)
            {
                configBuilder.AddCommandLine(env.AdditionalArguments);
            }

            // Use the global omnisharp config if there's any in the shared path
            configBuilder.CreateAndAddGlobalOptionsFile(env);

            // Use the local omnisharp config if there's any in the root path
            configBuilder.AddJsonFile(
                new PhysicalFileProvider(env.TargetDirectory).WrapForPolling(),
                Constants.OptionsFile,
                optional: true,
                reloadOnChange: true);

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

            var workspaceHelper = new WorkspaceHelper(PluginHost, Configuration, options.CurrentValue, loggerFactory);
            workspaceHelper.Initialize(Workspace);

            // when configuration options change
            // run workspace options providers automatically
            options.OnChange(o =>
            {
                workspaceHelper.ProvideOptions(Workspace, o);
            });

            logger.LogInformation("Configuration finished.");
        }

        private static List<Assembly> DiscoverOmniSharpAssemblies(IAssemblyLoader loader, ILogger logger)
        {
            // Iterate through all runtime libraries in the dependency context and
            // load them if they depend on OmniSharp.

            var assemblies = new List<Assembly>();
            var dependencyContext = DependencyContext.Default;

            foreach (var runtimeLibrary in dependencyContext.RuntimeLibraries)
            {
                if (DependsOnOmniSharp(runtimeLibrary))
                {
                    foreach (var name in runtimeLibrary.GetDefaultAssemblyNames(dependencyContext))
                    {
                        var assembly = loader.Load(name);
                        if (assembly != null)
                        {
                            assemblies.Add(assembly);

                            logger.LogDebug($"Loaded {assembly.FullName}");
                        }
                    }
                }
            }

            return assemblies;
        }

        private static bool DependsOnOmniSharp(RuntimeLibrary runtimeLibrary)
        {
            foreach (var dependency in runtimeLibrary.Dependencies)
            {
                if (dependency.Name == "OmniSharp.Abstractions" ||
                    dependency.Name == "OmniSharp.Roslyn")
                {
                    return true;
                }
            }

            return false;
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
