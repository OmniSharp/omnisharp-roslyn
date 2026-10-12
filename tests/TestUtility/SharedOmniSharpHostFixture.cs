using System;
using Xunit;

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
            _testOutput.Target = null;
            OmniSharpTestHost?.Dispose();
        }

        public ITestOutputHelper SetTestOutput(ITestOutputHelper output)
        {
            _testOutput.Target = output;
            return _testOutput;
        }

        public OmniSharpTestHost OmniSharpTestHost { get; set; }

        private sealed class DelegatingTestOutputHelper : ITestOutputHelper
        {
            public ITestOutputHelper Target { get; set; }

            public string Output => Target?.Output ?? string.Empty;

            public void Write(string message)
                => Target?.Write(message);

            public void Write(string format, params object[] args)
                => Target?.Write(format, args);

            // Background host logging can race with xUnit ending the current test.
            public void WriteLine(string message)
                => Target?.WriteLine(message);

            public void WriteLine(string format, params object[] args)
                => Target?.WriteLine(format, args);
        }
    }
}
