using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Stdio.Tests
{
    public class TestTextWriter : ISharedTextWriter
    {
        private readonly IEnumerator<Action<string>> _callbacks;
        private readonly TaskCompletionSource<object> _completion;

        public TestTextWriter(IEnumerable<Action<string>> callback)
        {
            _callbacks = callback.GetEnumerator();
            _callbacks.MoveNext();
            _completion = new TaskCompletionSource<object>();
        }

        public Task Completion
        {
            get { return _completion.Task; }
        }

        public void WriteLine(object value)
        {
            try
            {
                _callbacks.Current(value.ToString());
                
                if (!_callbacks.MoveNext())
                {
                    _completion.SetResult(null);
                }
            }
            catch (Exception e)
            {
                _completion.SetException(e);
            }
        }

        public Task WriteLineAsync(object value)
        {
            return Task.Factory.StartNew(() => WriteLine(value));
        }
    }
}
