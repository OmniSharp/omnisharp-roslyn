using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Tests
{
    class FakeServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IMemoryCache))
            {
            }
            if (serviceType == typeof(ILoggerFactory))
            {
                return new FakeLoggerFactory();
            }
            if (serviceType == typeof(IOmnisharpEnvironment))
            {
                return new FakeEnvironment();
            }
            if (serviceType == typeof(ISharedTextWriter))
            {
            }
            if (serviceType == typeof(IApplicationLifetime))
            {
            }
            return null;
        }
    }
}
