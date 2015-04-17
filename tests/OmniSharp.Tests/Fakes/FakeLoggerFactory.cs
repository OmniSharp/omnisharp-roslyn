using Microsoft.Framework.Logging;

namespace OmniSharp.Tests
{
    public class FakeLoggerFactory : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger Create(string name)
        {
            return NullLogger.Instance;
        }
    }
}
