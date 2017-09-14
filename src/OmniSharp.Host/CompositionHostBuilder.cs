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
using Microsoft.Extensions.Options;
using OmniSharp.Eventing;
using OmniSharp.FileWatching;
using OmniSharp.Mef;
using OmniSharp.Options;
using OmniSharp.Roslyn;
using OmniSharp.Services;
using OmniSharp.Stdio.Services;

namespace OmniSharp
{
    public class CompositionHostBuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOmniSharpEnvironment _environment;
        private readonly ISharedTextWriter _writer;
        private readonly IEventEmitter _eventEmitter;
        private readonly IEnumerable<Assembly> _assemblies;

        public CompositionHostBuilder(
            IServiceProvider serviceProvider,
            IOmniSharpEnvironment environment,
            ISharedTextWriter writer,
            IEventEmitter eventEmitter,
            IEnumerable<Assembly> assemblies = null)
        {
            _serviceProvider = serviceProvider;
            _environment = environment;
            _writer = writer;
            _eventEmitter = eventEmitter;
            _assemblies = assemblies ?? Array.Empty<Assembly>();
        }

        public CompositionHost Build()
        {
            var options = _serviceProvider.GetRequiredService<IOptionsMonitor<OmniSharpOptions>>();
            var memoryCache = _serviceProvider.GetRequiredService<IMemoryCache>();
            var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
            var assemblyLoader = _serviceProvider.GetRequiredService<IAssemblyLoader>();
            var config = new ContainerConfiguration();

            var assemblies = _assemblies
                .Concat(new[] { typeof(OmniSharpWorkspace).GetTypeInfo().Assembly, typeof(IRequest).GetTypeInfo().Assembly })
                .Distinct();

            config = config.WithAssemblies(assemblies);

            var fileSystemWatcher = new ManualFileSystemWatcher();
            var metadataHelper = new MetadataHelper(assemblyLoader);

            config = config
                .WithProvider(MefValueProvider.From(_serviceProvider))
                .WithProvider(MefValueProvider.From<IFileSystemWatcher>(fileSystemWatcher))
                .WithProvider(MefValueProvider.From(memoryCache))
                .WithProvider(MefValueProvider.From(loggerFactory))
                .WithProvider(MefValueProvider.From(_environment))
                .WithProvider(MefValueProvider.From(_writer))
                .WithProvider(MefValueProvider.From(options.CurrentValue))
                .WithProvider(MefValueProvider.From(options.CurrentValue.FormattingOptions))
                .WithProvider(MefValueProvider.From(assemblyLoader))
                .WithProvider(MefValueProvider.From(metadataHelper))
                .WithProvider(MefValueProvider.From(_eventEmitter ?? NullEventEmitter.Instance));

            return config.CreateContainer();
        }

        public static IServiceProvider CreateDefaultServiceProvider(IConfiguration configuration, IServiceCollection services = null)
        {
            services = services ?? new ServiceCollection();

            // Caching
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<IAssemblyLoader, AssemblyLoader>();
            services.AddOptions();

            // Setup the options from configuration
            services.Configure<OmniSharpOptions>(configuration);
            services.AddLogging();

            return services.BuildServiceProvider();
        }

        public CompositionHostBuilder WithOmniSharpAssemblies()
        {
            var assemblies = DiscoverOmniSharpAssemblies();

            return new CompositionHostBuilder(
                _serviceProvider, _environment, _writer, _eventEmitter, assemblies);
        }

        public CompositionHostBuilder WithAssemblies(params Assembly[] assemblies)
        {
            return new CompositionHostBuilder(
                _serviceProvider, _environment, _writer, _eventEmitter, assemblies);
        }

        private List<Assembly> DiscoverOmniSharpAssemblies()
        {
            var assemblyLoader = _serviceProvider.GetRequiredService<IAssemblyLoader>();
            var logger = _serviceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger<CompositionHostBuilder>();

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
                        var assembly = assemblyLoader.Load(name);
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
