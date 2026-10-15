using System;
using Xunit.Abstractions;

namespace TestUtility
{
    public class SharedOmniSharpHostFixture : IDisposable
    {
        private const string NoActiveTestMessage = "There is no currently active test.";
        private readonly DelegatingTestOutputHelper _testOutput = new DelegatingTestOutputHelper();

        public SharedOmniSharpHostFixture()
        {
        }

        static SharedOmniSharpHostFixture()
        {
            TestHelpers.SetDefaultCulture();
        }

        public void Dispose()
        {
            _testOutput.Output = null;
            OmniSharpTestHost?.Dispose();
        }

        public ITestOutputHelper SetTestOutput(ITestOutputHelper output)
        {
            _testOutput.Output = output;
            return _testOutput;
        }

        public OmniSharpTestHost OmniSharpTestHost { get; set; }

        private sealed class DelegatingTestOutputHelper : ITestOutputHelper
        {
            public ITestOutputHelper Output { get; set; }

            // Background host logging can race with xUnit ending the current test.
            public void WriteLine(string message)
            {
                try
                {
                    Output?.WriteLine(message);
                }
                catch (InvalidOperationException exception) when (exception.Message == NoActiveTestMessage)
                {
                }
            }

            public void WriteLine(string format, params object[] args)
            {
                try
                {
                    Output?.WriteLine(format, args);
                }
                catch (InvalidOperationException exception) when (exception.Message == NoActiveTestMessage)
                {
                }
            }
        }
    }
}
