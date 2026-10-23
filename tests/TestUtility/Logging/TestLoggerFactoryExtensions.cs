using Microsoft.Extensions.Logging;
using Xunit;

namespace TestUtility.Logging
{
    public static class TestLoggerFactoryExtensions
    {
        public static ILoggerFactory AddXunit(this ILoggerFactory factory, ITestOutputHelper output)
        {
            factory.AddProvider(new TestLoggerProvider(output));
            return factory;
        }
    }
}
