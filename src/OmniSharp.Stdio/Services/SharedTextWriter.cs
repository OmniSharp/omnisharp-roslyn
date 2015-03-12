using System.IO;
using System.Threading.Tasks;

namespace OmniSharp.Stdio.Services
{
    public class SharedTextWriter : ISharedTextWriter
    {
        private readonly object _lock = new object();
        private readonly TextWriter _writer;

        public SharedTextWriter(TextWriter writer)
        {
            _writer = writer;
        }

        public void WriteLine(object value)
        {
            lock(_lock)
            {
                _writer.WriteLine(value);
            }
        }

        public Task WriteLineAsync(object value)
        {
            return Task.Factory.StartNew(() => WriteLine(value));
        }
    }
}
