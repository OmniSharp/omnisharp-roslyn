using System;
using OmniSharp;

namespace TestUtility
{
    public class TestOmniSharp : IDisposable
    {
        private bool _disposed;

        public OmniSharpWorkspace Workspace { get; }

        ~TestOmniSharp()
        {
            throw new InvalidOperationException($"{nameof(TestOmniSharp)}.{nameof(Dispose)}() not called.");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                throw new InvalidOperationException($"{nameof(TestOmniSharp)} already disposed.");
            }

            this._disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
