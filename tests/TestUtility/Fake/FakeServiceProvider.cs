using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OmniSharp.Host.Loader;
using OmniSharp.Services;

namespace TestUtility.Fake
{
    internal class FakeServiceProvider : IServiceProvider
    {
        private readonly ILogger<FakeServiceProvider> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public FakeServiceProvider(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<FakeServiceProvider>();

            _services[typeof(ILoggerFactory)] = _loggerFactory;
            _services[typeof(IOmniSharpEnvironment)] = new FakeEnvironment();
            _services[typeof(IAssemblyLoader)] = new AssemblyLoader(_loggerFactory);
        }

        public object GetService(Type serviceType)
        {
            object result;
            if (!_services.TryGetValue(serviceType, out result))
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

        public void SetService(Type type, object instance)
        {
            _services[type] = instance;
        }
    }
}
