using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp;
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

        public TestServiceProvider(IOmniSharpEnvironment environment, ILoggerFactory loggerFactory, ISharedTextWriter sharedTextWriter, OmniSharpOptions options)
        {
            _logger = loggerFactory.CreateLogger<TestServiceProvider>();

            _services[typeof(IOptionsMonitor<OmniSharpOptions>)] = new OptionsMonitor<OmniSharpOptions>(
                Enumerable.Empty<IConfigureOptions<OmniSharpOptions>>(),
                Enumerable.Empty<IOptionsChangeTokenSource<OmniSharpOptions>>()
            );

            _services[typeof(OmniSharpOptions)] = options;
            _services[typeof(ILoggerFactory)] = loggerFactory;
            _services[typeof(IOmniSharpEnvironment)] = environment;
            _services[typeof(IAssemblyLoader)] = new AssemblyLoader(loggerFactory);
            _services[typeof(IMemoryCache)] = new MemoryCache(new MemoryCacheOptions());
            _services[typeof(ISharedTextWriter)] = sharedTextWriter;
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
