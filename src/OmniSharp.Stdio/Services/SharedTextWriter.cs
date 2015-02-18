using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Stdio.Services
{
    public class SharedTextWriter : ISharedTextWriter
    {
        private readonly AutoResetEvent _gate = new AutoResetEvent(true);
        private readonly TextWriter _writer;

        public SharedTextWriter(TextWriter writer)
        {
            _writer = writer;
        }

        public void Use(Action<TextWriter> callback)
        {
            try
            {
                _gate.WaitOne();
                callback(_writer);
            }
            finally
            {
                _gate.Set();
            }
        }

        public void Use(Func<TextWriter, Task> callback)
        {
            Task task;
            try
            {
                _gate.WaitOne();
                task = callback(_writer);

                // we wait for the task and when it is
                // done we release the lock here
                task.ContinueWith(_ =>
                {
                    _gate.Set();
                    if (task.Exception != null)
                    {
                        throw task.Exception;
                    }
                });
            }
            catch (Exception e)
            {
                // in case the callback failed to return
                // a proper task
                _gate.Set();
                throw e;
            }
        }
    }
}
