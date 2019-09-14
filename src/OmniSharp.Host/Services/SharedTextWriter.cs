using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Utilities;

namespace OmniSharp.Services
{
    public class SharedTextWriter : DisposableObject, ISharedTextWriter
    {
        private readonly TextWriter _writer;
        private readonly Thread _thread;
        private readonly BlockingCollection<object> _queue;
        private readonly CancellationTokenSource _cancel;

        public SharedTextWriter(TextWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _queue = new BlockingCollection<object>();
            _cancel = new CancellationTokenSource();
            _thread = new Thread(ProcessWriteQueue) { IsBackground = true, Name = "ProcessWriteQueue" };
            _thread.Start();
        }

        protected override void DisposeCore(bool disposing)
        {
            // Finish sending queued output to the writer.
            _cancel.Cancel();
            _thread.Join();
            _cancel.Dispose();
        }

        public void WriteLine(object value)
        {
            _queue.Add(value);
        }

        private void ProcessWriteQueue()
        {
            var token = _cancel.Token;
            try
            {
                while (true)
                {
                    if (_queue.TryTake(out var value, Timeout.Infinite, token))
                    {
                        _writer.WriteLine(value);
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                if (ex.CancellationToken != token)
                    throw;
                // else ignore. Exceptions: OperationCanceledException - The CancellationToken has been canceled.
            }
        }
    }
}
