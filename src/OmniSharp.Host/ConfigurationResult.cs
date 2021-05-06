using System;
using Microsoft.Extensions.Configuration;

namespace OmniSharp
{
    public class ConfigurationResult
    {
        public ConfigurationResult(IConfigurationRoot configuration)
        {
            Configuration = configuration;
        }

        public ConfigurationResult(Exception exception)
        {
            Exception = exception;
            Configuration = new ConfigurationRoot(Array.Empty<IConfigurationProvider>());
        }

        public IConfigurationRoot Configuration { get; }

        public Exception Exception { get; }

        public bool HasError() => Exception != null;
    }
}
