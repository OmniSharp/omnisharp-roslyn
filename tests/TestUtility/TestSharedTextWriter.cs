using System.Threading.Tasks;
using OmniSharp.Services;
using Xunit.Abstractions;

namespace TestUtility
{
    public class TestSharedTextWriter : ISharedTextWriter
    {
        private ITestOutputHelper _output;

        public TestSharedTextWriter(ITestOutputHelper output)
        {
            _output = output;
        }

        public void WriteLine(object value)
        {
            _output.WriteLine(value != null ? value.ToString() : string.Empty);
        }

        public Task WriteLineAsync(object value)
        {
            _output.WriteLine(value != null ? value.ToString() : string.Empty);

            return Task.CompletedTask;
        }
    }
}
