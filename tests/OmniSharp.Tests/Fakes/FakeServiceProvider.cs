using System;
using Microsoft.AspNet.Hosting;
using Microsoft.Framework.Caching.Memory;
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;
using OmniSharp.Options;
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
