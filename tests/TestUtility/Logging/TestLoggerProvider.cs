using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace TestUtility.Logging
{
    public class TestLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;

        public TestLoggerProvider(ITestOutputHelper output)
        {
            this._output = output;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(this._output, categoryName);
        }

        public void Dispose()
        {
        }
    }
}
