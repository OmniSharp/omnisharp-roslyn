using System;
using Xunit.Abstractions;

namespace TestUtility
{
    public class SharedOmniSharpHostFixture : IDisposable
    {
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

            public void WriteLine(string message)
                => Output?.WriteLine(message);

            public void WriteLine(string format, params object[] args)
                => Output?.WriteLine(format, args);
        }
    }
}
