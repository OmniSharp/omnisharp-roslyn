using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;
using TestUtility.Annotate;

namespace OmniSharp.Tests
{
    internal class TestServiceProvider : IServiceProvider
    {
        private readonly ILogger<TestServiceProvider> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public TestServiceProvider(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<TestServiceProvider>();

            _services[typeof(ILoggerFactory)] = _loggerFactory;
            _services[typeof(IOmnisharpEnvironment)] = new FakeEnvironment();
            _services[typeof(IOmnisharpAssemblyLoader)] = new AnnotateAssemblyLoader(_loggerFactory.CreateLogger<AnnotateAssemblyLoader>());
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
