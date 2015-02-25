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

        public Task Use(Func<TextWriter, Task> callback)
        {
            Task task;
            TaskCompletionSource<object> completion = new TaskCompletionSource<object>();
            try
            {
                _gate.WaitOne();
                task = callback(_writer);

                // we wait for the task and when it is
                // done we release the lock here
                return task.ContinueWith(_ =>
                {
                    _gate.Set();
                    if (task.Exception != null)
                    {
                        completion.SetException(task.Exception);
                    }
                    else
                    {
                        completion.SetResult(null);
                    }
                    return completion.Task;
                });
            }
            catch (Exception e)
            {
                // in case the callback failed to return
                // a proper task
                _gate.Set();
                completion.SetException(e);
                return completion.Task;
            }
        }
    }
}
