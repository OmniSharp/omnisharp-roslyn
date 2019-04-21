using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp;
using OmniSharp.Eventing;
using OmniSharp.Host.Services;
using OmniSharp.MSBuild.Discovery;
using OmniSharp.Options;
using OmniSharp.Services;
using OmniSharp.Utilities;
using TestUtility.Logging;
using Xunit.Abstractions;

namespace TestUtility
{
    public class TestServiceProvider : DisposableObject, IServiceProvider
    {
        private readonly ILogger<TestServiceProvider> _logger;
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        private TestServiceProvider(
            IOmniSharpEnvironment environment,
            ILoggerFactory loggerFactory,
            IAssemblyLoader assemblyLoader,
            IAnalyzerAssemblyLoader analyzerAssemblyLoader,
            IMemoryCache memoryCache,
            ISharedTextWriter sharedTextWriter,
            IMSBuildLocator msbuildLocator,
            IEventEmitter eventEmitter,
            IDotNetCliService dotNetCliService,
            IConfigurationRoot configuration,
            IOptionsMonitor<OmniSharpOptions> optionsMonitor)
        {
            _logger = loggerFactory.CreateLogger<TestServiceProvider>();

            AddService(environment);
            AddService(loggerFactory);
            AddService(assemblyLoader);
            AddService(memoryCache);
            AddService(sharedTextWriter);
            AddService(msbuildLocator);
            AddService(eventEmitter);
            AddService(dotNetCliService);
            AddService(configuration);
            AddService(optionsMonitor);
            AddService(analyzerAssemblyLoader);
        }

        public static IServiceProvider Create(
            ITestOutputHelper testOutput,
            IOmniSharpEnvironment environment,
            IEnumerable<KeyValuePair<string, string>> configurationData = null,
            DotNetCliVersion dotNetCliVersion = DotNetCliVersion.Current,
            IEventEmitter eventEmitter = null)
        {
            var loggerFactory = new LoggerFactory()
                .AddXunit(testOutput);

            eventEmitter = eventEmitter ?? NullEventEmitter.Instance;

            var assemblyLoader = CreateAssemblyLoader(loggerFactory);
            var dotNetCliService = CreateDotNetCliService(dotNetCliVersion, loggerFactory, eventEmitter);
            var configuration = CreateConfiguration(configurationData, dotNetCliService);
            var memoryCache = CreateMemoryCache();
            var msbuildLocator = CreateMSBuildLocator(loggerFactory, assemblyLoader);
            var optionsMonitor = CreateOptionsMonitor(configuration);
            var sharedTextWriter = CreateSharedTextWriter(testOutput);
            var analyzerAssemblyLoader = new AnalyzerAssemblyLoader();

            return new TestServiceProvider(
                environment, loggerFactory, assemblyLoader, analyzerAssemblyLoader, memoryCache, sharedTextWriter,
                msbuildLocator, eventEmitter, dotNetCliService, configuration, optionsMonitor);
        }

        public static IServiceProvider Create(
            ITestOutputHelper testOutput,
            IOmniSharpEnvironment environment,
            ILoggerFactory loggerFactory,
            IAssemblyLoader assemblyLoader,
            IAnalyzerAssemblyLoader analyzerAssemblyLoader,
            IMSBuildLocator msbuildLocator,
            IEnumerable<KeyValuePair<string, string>> configurationData = null,
            DotNetCliVersion dotNetCliVersion = DotNetCliVersion.Current,
            IEventEmitter eventEmitter = null)
        {
            eventEmitter = eventEmitter ?? NullEventEmitter.Instance;

            var dotNetCliService = CreateDotNetCliService(dotNetCliVersion, loggerFactory, eventEmitter);
            var configuration = CreateConfiguration(configurationData, dotNetCliService);
            var memoryCache = CreateMemoryCache();
            var optionsMonitor = CreateOptionsMonitor(configuration);
            var sharedTextWriter = CreateSharedTextWriter(testOutput);

            return new TestServiceProvider(
                environment, loggerFactory, assemblyLoader, analyzerAssemblyLoader, memoryCache, sharedTextWriter,
                msbuildLocator, eventEmitter, dotNetCliService, configuration, optionsMonitor);
        }

        private static IAssemblyLoader CreateAssemblyLoader(ILoggerFactory loggerFactory)
            => new AssemblyLoader(loggerFactory);

        private static IConfigurationRoot CreateConfiguration(IEnumerable<KeyValuePair<string, string>> configurationData, IDotNetCliService dotNetCliService)
        {
            var info = dotNetCliService.GetInfo();
            var msbuildSdksPath = Path.Combine(info.BasePath, "Sdks");

            var builder = new Microsoft.Extensions.Configuration.ConfigurationBuilder();

            builder.AddInMemoryCollection(configurationData);

            // We need to set the "UseLegacySdkResolver" for tests because
            // MSBuild's SDK resolver will not be able to locate the .NET Core SDKs
            // that we install locally in the ".dotnet" and ".dotnet-legacy" directories.
            // This property will cause the MSBuild project loader to set the
            // MSBuildSDKsPath environment variable to the correct path "Sdks" folder
            // within the appropriate .NET Core SDK.
            var msbuildProperties = new Dictionary<string, string>()
            {
                [$"MSBuild:{nameof(MSBuildOptions.UseLegacySdkResolver)}"] = "true",
                [$"MSBuild:{nameof(MSBuildOptions.MSBuildSDKsPath)}"] = msbuildSdksPath
            };

            builder.AddInMemoryCollection(msbuildProperties);

            return builder.Build();
        }

        private static IDotNetCliService CreateDotNetCliService(DotNetCliVersion dotNetCliVersion, ILoggerFactory loggerFactory, IEventEmitter eventEmitter)
        {
            var dotnetPath = Path.Combine(
                TestAssets.Instance.RootFolder,
                dotNetCliVersion.GetFolderName(),
                "dotnet");

            if (!File.Exists(dotnetPath))
            {
                dotnetPath = Path.ChangeExtension(dotnetPath, ".exe");
            }

            if (!File.Exists(dotnetPath))
            {
                throw new InvalidOperationException($"Local .NET CLI path does not exist. Did you run build.(ps1|sh) from the command line?");
            }

            return new DotNetCliService(loggerFactory, NullEventEmitter.Instance, dotnetPath);
        }

        private static IMemoryCache CreateMemoryCache()
            => new MemoryCache(new MemoryCacheOptions());

        private static IMSBuildLocator CreateMSBuildLocator(ILoggerFactory loggerFactory, IAssemblyLoader assemblyLoader)
            => MSBuildLocator.CreateStandAlone(loggerFactory, assemblyLoader, allowMonoPaths: false);

        private static IOptionsMonitor<OmniSharpOptions> CreateOptionsMonitor(IConfigurationRoot configurationRoot)
        {
            var setups = new IConfigureOptions<OmniSharpOptions>[]
            {
                new ConfigureOptions<OmniSharpOptions>(c => ConfigurationBinder.Bind(configurationRoot, c))
            };

            var factory = new OptionsFactory<OmniSharpOptions>(
                setups,
                postConfigures: Enumerable.Empty<IPostConfigureOptions<OmniSharpOptions>>()
            );

            return new OptionsMonitor<OmniSharpOptions>(
                factory,
                sources: Enumerable.Empty<IOptionsChangeTokenSource<OmniSharpOptions>>(),
                cache: new OptionsCache<OmniSharpOptions>()
            );
        }

        private static ISharedTextWriter CreateSharedTextWriter(ITestOutputHelper testOutput)
            => new TestSharedTextWriter(testOutput);

        ~TestServiceProvider()
        {
            throw new InvalidOperationException($"{nameof(TestServiceProvider)}.{nameof(Dispose)}() not called.");
        }

        private void AddService<TServiceType>(TServiceType instance)
        {
            _services[typeof(TServiceType)] = instance;
        }

        protected override void DisposeCore(bool disposing)
        {
            foreach (var service in _services.Values)
            {
                if (service is IDisposable d)
                {
                    d.Dispose();
                }
            }
        }

        public object GetService(Type serviceType)
        {
            if (!_services.TryGetValue(serviceType, out var result))
            {
                result = null;
            }

            if (result == null)
            {
                _logger.LogWarning($"{nameof(GetService)}: {serviceType.Name} => null");
            }
            else
            {
                _logger.LogInformation($"{nameof(GetService)}: {serviceType.Name} => {result.GetType().Name}");
            }

            return result;
        }
    }
}
