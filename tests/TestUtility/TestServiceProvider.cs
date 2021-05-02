using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp;
using OmniSharp.Eventing;
using OmniSharp.FileWatching;
using OmniSharp.Host.Services;
using OmniSharp.MSBuild.Discovery;
using OmniSharp.Options;
using OmniSharp.Services;
using OmniSharp.Utilities;
using TestUtility.Logging;
using Xunit.Abstractions;
using ConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;

namespace TestUtility
{
    public class TestServiceProvider : DisposableObject, IServiceProvider
    {
        private readonly ILogger<TestServiceProvider> _logger;
        private readonly ServiceProvider _serviceProvider;
        private readonly IServiceCollection _services;

        private TestServiceProvider(
            IOmniSharpEnvironment environment,
            ILoggerFactory loggerFactory,
            IAssemblyLoader assemblyLoader,
            IAnalyzerAssemblyLoader analyzerAssemblyLoader,
            ISharedTextWriter sharedTextWriter,
            IMSBuildLocator msbuildLocator,
            IEventEmitter eventEmitter,
            IDotNetCliService dotNetCliService,
            IConfigurationRoot configuration)
        {
            _logger = loggerFactory.CreateLogger<TestServiceProvider>();
            var services = _services = new ServiceCollection();
            services
                .AddLogging()
                .AddOptions()
                .AddMemoryCache();

            services
                .AddSingleton(environment)
                .AddSingleton(loggerFactory)
                .AddSingleton(assemblyLoader)
                .AddSingleton(sharedTextWriter)
                .AddSingleton(msbuildLocator)
                .AddSingleton(eventEmitter)
                .AddSingleton(dotNetCliService)
                .AddSingleton(configuration)
                .AddSingleton(configuration as IConfiguration)
                .Configure<OmniSharpOptions>(configuration)
                .PostConfigure<OmniSharpOptions>(OmniSharpOptions.PostConfigure)
                .AddSingleton(analyzerAssemblyLoader);

            services.TryAddSingleton(_ => new ManualFileSystemWatcher());
            services.TryAddSingleton<IFileSystemNotifier>(_ => _.GetRequiredService<ManualFileSystemWatcher>());
            services.TryAddSingleton<IFileSystemWatcher>(_ => _.GetRequiredService<ManualFileSystemWatcher>());

            _serviceProvider = services.BuildServiceProvider();
        }

        public static IServiceProvider Create(
            ITestOutputHelper testOutput,
            IOmniSharpEnvironment environment,
            IConfiguration configurationData = null,
            DotNetCliVersion dotNetCliVersion = DotNetCliVersion.Current,
            IEventEmitter eventEmitter = null)
        {
            var loggerFactory = new LoggerFactory()
                .AddXunit(testOutput);

            eventEmitter = eventEmitter ?? NullEventEmitter.Instance;

            var assemblyLoader = CreateAssemblyLoader(loggerFactory);
            var dotNetCliService = CreateDotNetCliService(dotNetCliVersion, loggerFactory, eventEmitter);
            var configuration = CreateConfiguration(configurationData);
            var msbuildLocator = CreateMSBuildLocator(loggerFactory, assemblyLoader);
            var sharedTextWriter = CreateSharedTextWriter(testOutput);
            var analyzerAssemblyLoader = new AnalyzerAssemblyLoader();

            return new TestServiceProvider(
                environment, loggerFactory, assemblyLoader, analyzerAssemblyLoader, sharedTextWriter,
                msbuildLocator, eventEmitter, dotNetCliService, configuration);
        }

        public static IServiceProvider Create(
            ITestOutputHelper testOutput,
            IOmniSharpEnvironment environment,
            ILoggerFactory loggerFactory,
            IAssemblyLoader assemblyLoader,
            IAnalyzerAssemblyLoader analyzerAssemblyLoader,
            IMSBuildLocator msbuildLocator,
            IConfiguration configurationData = null,
            DotNetCliVersion dotNetCliVersion = DotNetCliVersion.Current,
            IEventEmitter eventEmitter = null)
        {
            eventEmitter = eventEmitter ?? NullEventEmitter.Instance;

            var dotNetCliService = CreateDotNetCliService(dotNetCliVersion, loggerFactory, eventEmitter);
            var configuration = CreateConfiguration(configurationData);
            var sharedTextWriter = CreateSharedTextWriter(testOutput);

            return new TestServiceProvider(
                environment, loggerFactory, assemblyLoader, analyzerAssemblyLoader, sharedTextWriter,
                msbuildLocator, eventEmitter, dotNetCliService, configuration);
        }

        private static IAssemblyLoader CreateAssemblyLoader(ILoggerFactory loggerFactory)
            => new AssemblyLoader(loggerFactory);

        private static IConfigurationRoot CreateConfiguration(IConfiguration configurationData)
        {
            var builder = new ConfigurationBuilder();

            if (configurationData != null)
            {
                builder.AddConfiguration(configurationData);
            }

            // We need to set the "UseLegacySdkResolver" for tests because
            // MSBuild's SDK resolver will not be able to locate the .NET Core SDKs
            // that we install locally in the ".dotnet" directory.
            // This property will cause the MSBuild project loader to set the
            // MSBuildSDKsPath environment variable to the correct path "Sdks" folder
            // within the appropriate .NET Core SDK.
            var msbuildProperties = new Dictionary<string, string>()
            {
                [$"MSBuild:{nameof(MSBuildOptions.UseLegacySdkResolver)}"] = "true"
            };

            builder.AddInMemoryCollection(msbuildProperties);

            return builder.Build();
        }

        private static IDotNetCliService CreateDotNetCliService(DotNetCliVersion dotNetCliVersion,
            ILoggerFactory loggerFactory, IEventEmitter eventEmitter)
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
                throw new InvalidOperationException(
                    $"Local .NET CLI path does not exist. Did you run build.(ps1|sh) from the command line?");
            }

            return new DotNetCliService(loggerFactory, NullEventEmitter.Instance, dotnetPath);
        }

        private static IMSBuildLocator CreateMSBuildLocator(ILoggerFactory loggerFactory,
            IAssemblyLoader assemblyLoader)
            => MSBuildLocator.CreateStandAlone(loggerFactory, assemblyLoader);

        private static ISharedTextWriter CreateSharedTextWriter(ITestOutputHelper testOutput)
            => new TestSharedTextWriter(testOutput);

        ~TestServiceProvider()
        {
            throw new InvalidOperationException($"{nameof(TestServiceProvider)}.{nameof(Dispose)}() not called.");
        }

        protected override void DisposeCore(bool disposing)
        {
            _serviceProvider.Dispose();
            foreach (var service in _services)
            {
                if (service.ImplementationInstance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        public object GetService(Type serviceType)
        {
            var result = _serviceProvider.GetService(serviceType);

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
