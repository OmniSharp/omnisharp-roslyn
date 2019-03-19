using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Composition.Hosting.Core;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Eventing;
using OmniSharp.FileSystem;
using OmniSharp.FileWatching;
using OmniSharp.Mef;
using OmniSharp.MSBuild.Discovery;
using OmniSharp.Options;
using OmniSharp.Roslyn;
using OmniSharp.Services;

namespace OmniSharp
{
    public class CompositionHostBuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<Assembly> _assemblies;
        private readonly IEnumerable<ExportDescriptorProvider> _exportDescriptorProviders;

        public CompositionHostBuilder(
            IServiceProvider serviceProvider,
            IEnumerable<Assembly> assemblies = null,
            IEnumerable<ExportDescriptorProvider> exportDescriptorProviders = null)
        {
            _serviceProvider = serviceProvider;
            _assemblies = assemblies ?? Array.Empty<Assembly>();
            _exportDescriptorProviders = exportDescriptorProviders ?? Array.Empty<ExportDescriptorProvider>();
        }

        public CompositionHost Build()
        {
            var options = _serviceProvider.GetRequiredService<IOptionsMonitor<OmniSharpOptions>>();
            var memoryCache = _serviceProvider.GetRequiredService<IMemoryCache>();
            var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
            var assemblyLoader = _serviceProvider.GetRequiredService<IAssemblyLoader>();
            var analyzerAssemblyLoader = _serviceProvider.GetRequiredService<IAnalyzerAssemblyLoader>();
            var environment = _serviceProvider.GetRequiredService<IOmniSharpEnvironment>();
            var eventEmitter = _serviceProvider.GetRequiredService<IEventEmitter>();
            var dotNetCliService = _serviceProvider.GetRequiredService<IDotNetCliService>();
            var config = new ContainerConfiguration();

            var fileSystemWatcher = new ManualFileSystemWatcher();
            var metadataHelper = new MetadataHelper(assemblyLoader);

            var logger = loggerFactory.CreateLogger<CompositionHostBuilder>();

            // We must register an MSBuild instance before composing MEF to ensure that
            // our AssemblyResolve event is hooked up first.
            var msbuildLocator = _serviceProvider.GetRequiredService<IMSBuildLocator>();

            // Don't register the default instance if an instance is already registered!
            // This is for tests, where the MSBuild instance may be registered early.
            if (msbuildLocator.RegisteredInstance == null)
            {
                msbuildLocator.RegisterDefaultInstance(logger);
            }

            config = config
                .WithProvider(MefValueProvider.From(_serviceProvider))
                .WithProvider(MefValueProvider.From<IFileSystemNotifier>(fileSystemWatcher))
                .WithProvider(MefValueProvider.From<IFileSystemWatcher>(fileSystemWatcher))
                .WithProvider(MefValueProvider.From(memoryCache))
                .WithProvider(MefValueProvider.From(loggerFactory))
                .WithProvider(MefValueProvider.From(environment))
                .WithProvider(MefValueProvider.From(options.CurrentValue))
                .WithProvider(MefValueProvider.From(options.CurrentValue.FormattingOptions))
                .WithProvider(MefValueProvider.From(assemblyLoader))
                .WithProvider(MefValueProvider.From(analyzerAssemblyLoader))
                .WithProvider(MefValueProvider.From(dotNetCliService))
                .WithProvider(MefValueProvider.From(metadataHelper))
                .WithProvider(MefValueProvider.From(msbuildLocator))
                .WithProvider(MefValueProvider.From(eventEmitter));

            foreach (var exportDescriptorProvider in _exportDescriptorProviders)
            {
                config = config.WithProvider(exportDescriptorProvider);
            }

            var parts = _assemblies
                .Concat(new[] { typeof(OmniSharpWorkspace).GetTypeInfo().Assembly, typeof(IRequest).GetTypeInfo().Assembly, typeof(FileSystemHelper).GetTypeInfo().Assembly })
                .Distinct()
                .SelectMany(a => SafeGetTypes(a))
                .ToArray();

            config = config.WithParts(parts);

            return config.CreateContainer();
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

        public static IServiceProvider CreateDefaultServiceProvider(
            IOmniSharpEnvironment environment,
            IConfigurationRoot configuration,
            IEventEmitter eventEmitter,
            IServiceCollection services = null,
            Action<ILoggingBuilder> configureLogging = null)
        {
            services = services ?? new ServiceCollection();

            services.AddSingleton(environment);
            services.AddSingleton(eventEmitter);

            // Caching
            services.AddSingleton<IMemoryCache, MemoryCache>();
            services.AddSingleton<IAssemblyLoader, AssemblyLoader>();
            services.AddSingleton<IAnalyzerAssemblyLoader, AssemblyLoader>();
            services.AddOptions();

            services.AddSingleton<IDotNetCliService, DotNetCliService>();

            // MSBuild
            services.AddSingleton<IMSBuildLocator>(sp =>
                MSBuildLocator.CreateDefault(
                    loggerFactory: sp.GetService<ILoggerFactory>(),
                    assemblyLoader: sp.GetService<IAssemblyLoader>()));

            // Setup the options from configuration
            services.Configure<OmniSharpOptions>(configuration);
            services.AddSingleton(configuration);

            services.AddLogging(builder =>
            {
                var workspaceInformationServiceName = typeof(WorkspaceInformationService).FullName;
                var projectEventForwarder = typeof(ProjectEventForwarder).FullName;

                builder.AddFilter(
                    (category, logLevel) =>
                        environment.LogLevel <= logLevel &&
                        category.StartsWith("OmniSharp", StringComparison.OrdinalIgnoreCase) &&
                        !category.Equals(workspaceInformationServiceName, StringComparison.OrdinalIgnoreCase) &&
                        !category.Equals(projectEventForwarder, StringComparison.OrdinalIgnoreCase));

                configureLogging?.Invoke(builder);
            });

            return services.BuildServiceProvider();
        }

        public CompositionHostBuilder WithOmniSharpAssemblies()
        {
            var assemblies = DiscoverOmniSharpAssemblies();

            return new CompositionHostBuilder(
                _serviceProvider,
                _assemblies.Concat(assemblies).Distinct()
            );
        }

        public CompositionHostBuilder WithAssemblies(params Assembly[] assemblies)
        {
            return new CompositionHostBuilder(
                _serviceProvider,
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
                    dependency.Name == "OmniSharp.Shared" ||
                    dependency.Name == "OmniSharp.Roslyn")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
