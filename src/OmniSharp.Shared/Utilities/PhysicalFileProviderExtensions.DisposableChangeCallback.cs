using System;

namespace OmniSharp.Utilities
{
    public static partial class PhysicalFileProviderExtensions
    {
        private partial class PollingFileChangeToken
        {
            private class DisposableChangeCallback : IDisposable
            {
                private PollingFileChangeToken _changeToken;
                private int _callbackId;

                public DisposableChangeCallback(PollingFileChangeToken pollingFileChangeToken, int callbackId)
                {
                    _changeToken = pollingFileChangeToken;
                    _callbackId = callbackId;
                }

                public void Dispose()
                {
                    _changeToken.UnregisterChangeCallback(_callbackId);
                }
            }
        }
    }
}
