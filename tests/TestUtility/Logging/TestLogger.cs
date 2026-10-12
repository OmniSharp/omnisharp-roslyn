using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OmniSharp.Logging;
using Xunit;

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
                _output?.WriteLine(message);
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "There is no currently active test.")
            {
                // Process output can arrive after xUnit completes the test.
                Trace.WriteLine(message);
            }
        }
    }
}
