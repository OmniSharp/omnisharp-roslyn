using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp;
using OmniSharp.Eventing;
using OmniSharp.MSBuild.Discovery;
using OmniSharp.Options;
using OmniSharp.Services;
using OmniSharp.Stdio.Services;
using OmniSharp.Utilities;

namespace TestUtility
{
    public class TestServiceProvider : DisposableObject, IServiceProvider
    {
        private readonly ILogger<TestServiceProvider> _logger;
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public TestServiceProvider(
            IOmniSharpEnvironment environment,
            ILoggerFactory loggerFactory,
            ISharedTextWriter sharedTextWriter,
            IConfiguration configuration,
            IEventEmitter eventEmitter,
            IDotNetCliService dotNetCliService = null)
        {
            _logger = loggerFactory.CreateLogger<TestServiceProvider>();

            _services[typeof(IOptionsMonitor<OmniSharpOptions>)] = new OptionsMonitor<OmniSharpOptions>(
                new IConfigureOptions<OmniSharpOptions>[] {
                    new ConfigureOptions<OmniSharpOptions>(c => ConfigurationBinder.Bind(configuration, c))
                },
                Enumerable.Empty<IOptionsChangeTokenSource<OmniSharpOptions>>()
            );

            var assemblyLoader = new AssemblyLoader(loggerFactory);
            var msbuildLocator = MSBuildLocator.CreateStandAlone(loggerFactory, assemblyLoader, allowMonoPaths: false);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            dotNetCliService = dotNetCliService ?? new DotNetCliService(loggerFactory, eventEmitter);

            _services[typeof(ILoggerFactory)] = loggerFactory;
            _services[typeof(IOmniSharpEnvironment)] = environment;
            _services[typeof(IAssemblyLoader)] = assemblyLoader;
            _services[typeof(IMemoryCache)] = memoryCache;
            _services[typeof(ISharedTextWriter)] = sharedTextWriter;
            _services[typeof(IMSBuildLocator)] = msbuildLocator;
            _services[typeof(IEventEmitter)] = eventEmitter;
            _services[typeof(IDotNetCliService)] = dotNetCliService;
        }

        ~TestServiceProvider()
        {
            throw new InvalidOperationException($"{nameof(TestServiceProvider)}.{nameof(Dispose)}() not called.");
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
                _logger.LogWarning($"GetSerivce {serviceType.Name} => {result?.GetType()?.Name ?? "null"}");
            }
            else
            {
                _logger.LogInformation($"GetSerivce {serviceType.Name} => {result?.GetType()?.Name ?? "null"}");
            }

            return result;
        }
    }
}
