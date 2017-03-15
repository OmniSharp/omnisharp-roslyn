using System;

namespace OmniSharp.Utilities
{
    public abstract class DisposableObject : IDisposable
    {
        private bool _isDisposed;

        public bool IsDisposed => _isDisposed;

        ~DisposableObject()
        {
            DisposeCore(false);
        }

        protected abstract void DisposeCore(bool disposing);

        public void Dispose()
        {
            DisposeCore(true);
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
