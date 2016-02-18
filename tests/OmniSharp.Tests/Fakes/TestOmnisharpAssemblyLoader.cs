using System.Reflection;
using Microsoft.Extensions.Logging;
using OmniSharp.Host.Loader;

namespace OmniSharp.Tests
{
    public class TestOmnisharpAssemblyLoader : OmnisharpAssemblyLoader
    {
        private readonly ILogger _logger;

        public TestOmnisharpAssemblyLoader(ILogger logger)
        {
            _logger = logger;
        }

        public override Assembly Load(AssemblyName name)
        {
            _logger?.LogInformation($"{nameof(TestOmnisharpAssemblyLoader)}: Loading assembly {name}");
            return base.Load(name);
        }
    }
}
