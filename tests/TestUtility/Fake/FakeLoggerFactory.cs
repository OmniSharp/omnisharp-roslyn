using Microsoft.Extensions.Logging;

namespace TestUtility.Fake
{
    public class FakeLoggerFactory : ILoggerFactory
    {
        private static FakeLogger logger = new FakeLogger();

        public void AddProvider(ILoggerProvider provider) { }

        public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        public ILogger CreateLogger(string name) => logger;

        public void Dispose() { }
    }
}
