using System;
using System.IO;
using System.Threading.Tasks;
using OmniSharp.Utilities;

namespace OmniSharp.Stdio.Services
{
    public class SharedTextWriter : DisposableObject, ISharedTextWriter
    {
        private readonly object _lock = new object();
        private readonly TextWriter _writer;
        private Task _task = Task.CompletedTask;

        public SharedTextWriter(TextWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        protected override void DisposeCore(bool disposing)
        {
            // Finish sending queued output to the writer.
            _task.Wait();
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
            lock (_lock)
            {
                return _task = _task.ContinueWith(_ =>
                {
                    WriteLine(value);
                },
                TaskScheduler.Default);
            }
        }
    }
}
