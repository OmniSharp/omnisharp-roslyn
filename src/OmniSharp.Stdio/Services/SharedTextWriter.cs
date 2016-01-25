using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Stdio.Services
{
    public class SharedTextWriter : ISharedTextWriter
    {
        private BlockingCollection<object> _queue = new BlockingCollection<object>();

        private readonly object _lock = new object();
        private readonly TextWriter _writer;

        public SharedTextWriter(TextWriter writer)
        {
            _writer = writer;

            var thread = new Thread(() => { while (true) WriteLine(_queue.Take()); })
            {
                Name = $"{nameof(SharedTextWriter)} {nameof(BlockingCollection<object>)}",
                IsBackground = true
            };

            thread.Start();
        }

        public void WriteLine(object value)
        {
            lock (_lock)
            {
                _writer.WriteLine(value);
            }
        }

        public Task WriteLineAsync(object value)
        {
            _queue.Add(value);

            return Task.FromResult(0);
        }
    }
}
