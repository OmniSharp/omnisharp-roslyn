using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Services;

namespace OmniSharp.Stdio.Tests
{
    public class TestTextWriter : ISharedTextWriter
    {
        private readonly IEnumerator<Action<string>> _callbacks;
        private readonly ManualResetEvent _completion;

        public WaitHandle Completion => _completion;
        public Exception Exception { get; private set; }

        public TestTextWriter(params Action<string>[] callback)
        {
            _callbacks = new List<Action<string>>(callback).GetEnumerator();
            _callbacks.MoveNext();
            _completion = new ManualResetEvent(false);
        }

        public void WriteLine(object value)
        {
            try
            {
                _callbacks.Current(value.ToString());

                if (!_callbacks.MoveNext())
                {
                    _completion.Set();
                }
            }
            catch (Exception e)
            {
                Exception = e;
                _completion.Set();
            }
        }

        public Task WriteLineAsync(object value)
        {
            return Task.Factory.StartNew(() => WriteLine(value));
        }
    }
}
