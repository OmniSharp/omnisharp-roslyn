using Microsoft.Extensions.Logging;
using OmniSharp.Logging;
using Xunit.Abstractions;

namespace TestUtility.Logging
{
    public class TestLogger : BaseLogger
    {
        private readonly ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output, string categoryName)
            : base(categoryName)
        {
            this._output = output;
        }

        protected override void WriteMessage(LogLevel logLevel, string message)
        {
            try
            {
                _output.WriteLine(message);
            }
            catch
            {
                // This often causes error on debugging during dispose.
            }
        }
    }
}
