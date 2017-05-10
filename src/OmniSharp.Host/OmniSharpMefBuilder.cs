using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.FileWatching;
using OmniSharp.Host.Loader;
using OmniSharp.Mef;
using OmniSharp.Options;
using OmniSharp.Roslyn;
using OmniSharp.Services;
using OmniSharp.Stdio.Services;

namespace OmniSharp
{
    public class OmniSharpMefBuilder
    {
        private readonly IOmniSharpEnvironment _environment;
        private readonly ISharedTextWriter _writer;
        private readonly IAssemblyLoader _assemblyLoader;
        private readonly IConfiguration _configuration;

        public OmniSharpMefBuilder(
            IOmniSharpEnvironment environment,
            ISharedTextWriter writer,
            IAssemblyLoader assemblyLoader,
            IConfiguration configuration)
        {
            _environment = environment;
            _writer = writer;
            _assemblyLoader = assemblyLoader;
            _configuration = configuration;
        }

        public (IServiceProvider serviceProvider, CompositionHost compositionHost) Build()
        {
            var serviceProvider = GetServiceProvider();
            var logger = ServiceProviderServiceExtensions.GetRequiredService<Logger<OmniSharpMefBuilder>>(serviceProvider);
            var options = ServiceProviderServiceExtensions.GetRequiredService<OmniSharpOptions>(serviceProvider);
            var memoryCache = ServiceProviderServiceExtensions.GetRequiredService<IMemoryCache>(serviceProvider);
            var loggerFactory = ServiceProviderServiceExtensions.GetRequiredService<ILoggerFactory>(serviceProvider);

            var config = new ContainerConfiguration();

            foreach (var assembly in Enumerable.Concat(DiscoverOmniSharpAssemblies(_assemblyLoader, logger), new[] { typeof(OmniSharpWorkspace).GetTypeInfo().Assembly, typeof(IRequest).GetTypeInfo().Assembly })
                .Distinct())
            {
                config = config.WithAssembly(assembly);
            }

            var fileSystemWatcher = new ManualFileSystemWatcher();
            var metadataHelper = new MetadataHelper(_assemblyLoader);

            config = config
                .WithProvider(MefValueProvider.From<IServiceProvider>(serviceProvider))
                .WithProvider(MefValueProvider.From<IFileSystemWatcher>(fileSystemWatcher))
                .WithProvider(MefValueProvider.From(memoryCache))
                .WithProvider(MefValueProvider.From(loggerFactory))
                .WithProvider(MefValueProvider.From<IOmniSharpEnvironment>(_environment))
                .WithProvider(MefValueProvider.From<ISharedTextWriter>(_writer))
                .WithProvider(MefValueProvider.From(options))
                .WithProvider(MefValueProvider.From(options.FormattingOptions))
                .WithProvider(MefValueProvider.From<IAssemblyLoader>(_assemblyLoader))
                .WithProvider(MefValueProvider.From(metadataHelper));

            if (_environment.TransportType == TransportType.Stdio)
            {
                config = config
                    .WithProvider(MefValueProvider.From<IEventEmitter>(new StdioEventEmitter(_writer)));
            }
            else
            {
                config = config
                    .WithProvider(MefValueProvider.From(NullEventEmitter.Instance));
            }

            return (serviceProvider, config.CreateContainer());
        }

        private IServiceProvider GetServiceProvider()
        {
            IServiceCollection services = new ServiceCollection();

            // Caching
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<IAssemblyLoader, AssemblyLoader>();
            services.AddOptions();

            // Setup the options from configuration
            OptionsConfigurationServiceCollectionExtensions.Configure<OmniSharpOptions>(services, _configuration);
            services.AddLogging();

            return services.BuildServiceProvider();
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
    }
}