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
using OmniSharp.MSBuild.Discovery;
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
        private readonly IEventEmitter _eventEmitter;
        private readonly IEnumerable<Assembly> _assemblies;

        public CompositionHostBuilder(
            IServiceProvider serviceProvider,
            IOmniSharpEnvironment environment,
            IEventEmitter eventEmitter,
            IEnumerable<Assembly> assemblies = null)
        {
            _serviceProvider = serviceProvider;
            _environment = environment;
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

            var fileSystemWatcher = new ManualFileSystemWatcher();
            var metadataHelper = new MetadataHelper(assemblyLoader);

            var logger = loggerFactory.CreateLogger<CompositionHostBuilder>();

            // We must register an MSBuild instance before composing MEF to ensure that
            // our AssemblyResolve event is hooked up first.
            var msbuildLocator = _serviceProvider.GetRequiredService<IMSBuildLocator>();

            RegisterMSBuildInstance(msbuildLocator, logger);

            config = config
                .WithProvider(MefValueProvider.From(_serviceProvider))
                .WithProvider(MefValueProvider.From<IFileSystemNotifier>(fileSystemWatcher))
                .WithProvider(MefValueProvider.From<IFileSystemWatcher>(fileSystemWatcher))
                .WithProvider(MefValueProvider.From(memoryCache))
                .WithProvider(MefValueProvider.From(loggerFactory))
                .WithProvider(MefValueProvider.From(_environment))
                .WithProvider(MefValueProvider.From(options.CurrentValue))
                .WithProvider(MefValueProvider.From(options.CurrentValue.FormattingOptions))
                .WithProvider(MefValueProvider.From(assemblyLoader))
                .WithProvider(MefValueProvider.From(metadataHelper))
                .WithProvider(MefValueProvider.From(msbuildLocator))
                .WithProvider(MefValueProvider.From(_eventEmitter ?? NullEventEmitter.Instance));

            var parts = _assemblies
                .Concat(new[] { typeof(OmniSharpWorkspace).GetTypeInfo().Assembly, typeof(IRequest).GetTypeInfo().Assembly })
                .Distinct()
                .SelectMany(a => SafeGetTypes(a))
                .ToArray();

            config = config.WithParts(parts);

            return config.CreateContainer();
        }

        private static void RegisterMSBuildInstance(IMSBuildLocator msbuildLocator, ILogger logger)
        {
            MSBuildInstance instanceToRegister = null;
            var invalidVSFound = false;

            foreach (var instance in msbuildLocator.GetInstances())
            {
                if (instance.IsInvalidVisualStudio())
                {
                    invalidVSFound = true;
                }
                else
                {
                    instanceToRegister = instance;
                    break;
                }
            }


            if (instanceToRegister != null)
            {
                // Did we end up choosing the standalone MSBuild because there was an invalid Visual Studio?
                // If so, provide a helpful message to the user.
                if (invalidVSFound && instanceToRegister.DiscoveryType == DiscoveryType.StandAlone)
                {
                    logger.LogWarning(@"It looks like you have Visual Studio 2017 RTM installed.
Try updating Visual Studio 2017 to the most recent release to enable better MSBuild support.");
                }

                msbuildLocator.RegisterInstance(instanceToRegister);
            }
            else
            {
                logger.LogError("Could not locate MSBuild instance to register with OmniSharp");
            }
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try
            {
                return a.DefinedTypes.Select(t => t.AsType()).ToArray();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null).ToArray();
            }
        }

        public static IServiceProvider CreateDefaultServiceProvider(IConfiguration configuration, IServiceCollection services = null)
        {
            services = services ?? new ServiceCollection();

            // Caching
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<IAssemblyLoader, AssemblyLoader>();
            services.AddOptions();

            // MSBuild
            services.AddSingleton<IMSBuildLocator>(sp =>
                MSBuildLocator.CreateDefault(
                    loggerFactory: sp.GetService<ILoggerFactory>(),
                    assemblyLoader: sp.GetService<IAssemblyLoader>()));

            // Setup the options from configuration
            services.Configure<OmniSharpOptions>(configuration);
            services.AddLogging();

            return services.BuildServiceProvider();
        }

        public CompositionHostBuilder WithOmniSharpAssemblies()
        {
            var assemblies = DiscoverOmniSharpAssemblies();

            return new CompositionHostBuilder(
                _serviceProvider,
                _environment,
                _eventEmitter,
                _assemblies.Concat(assemblies).Distinct()
            );
        }

        public CompositionHostBuilder WithAssemblies(params Assembly[] assemblies)
        {
            return new CompositionHostBuilder(
                _serviceProvider,
                _environment,
                _eventEmitter,
                _assemblies.Concat(assemblies).Distinct()
            );
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
