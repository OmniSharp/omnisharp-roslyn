using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace OmniSharp.Utilities
{
    public static partial class PhysicalFileProviderExtensions
    {
        private partial class PollingFileChangeToken : DisposableObject, IChangeToken
        {
            private readonly static TimeSpan s_pollingInterval = TimeSpan.FromSeconds(2);

            private bool _polling;

            private readonly string _filePath;
            private bool _hasChanged;
            private DateTime _previousLastWriteTimeUtc;

            private readonly object _gate = new object();
            private int _nextCallbackId = 1;
            private Dictionary<int, Action<object>> _callbacks;

            public PollingFileChangeToken(string filePath)
            {
                _filePath = filePath;
                _previousLastWriteTimeUtc = GetLastWriteTimeUtc();
            }

            protected override void DisposeCore(bool disposing)
            {
                if (disposing)
                {
                    lock (_gate)
                    {
                        _polling = false;
                        _callbacks.Clear();
                        _callbacks = null;
                    }
                }
            }

            private DateTime GetLastWriteTimeUtc() =>
                File.Exists(_filePath)
                    ? File.GetLastWriteTimeUtc(_filePath)
                    : DateTime.MinValue;

            private void StartPolling()
            {
                if (_polling)
                {
                    return;
                }

                _polling = true;

                Task.Run(async () =>
                {
                    while (_polling)
                    {
                        await Task.Delay(s_pollingInterval);

                        lock (_gate)
                        {
                            var lastWriteTimeUtc = GetLastWriteTimeUtc();
                            if (_previousLastWriteTimeUtc != lastWriteTimeUtc)
                            {
                                _previousLastWriteTimeUtc = lastWriteTimeUtc;
                                _hasChanged = true;

                                // Notify callbacks.
                                if (_callbacks != null)
                                {
                                    // Once a callback is notified, it is removed from the list. It is up to the callback to
                                    // re-register itself.
                                    var callbacks = _callbacks.ToArray();

                                    foreach (var kvp in callbacks)
                                    {
                                        _callbacks.Remove(kvp.Key);
                                        kvp.Value(null);
                                    }
                                }
                            }
                        }
                    }
                });
            }

            private void StopPolling()
            {
                _polling = false;
            }

            public bool HasChanged
            {
                get
                {
                    lock (_gate)
                    {
                        return _hasChanged;
                    }
                }
            }

            public bool ActiveChangeCallbacks
            {
                get
                {
                    lock (_gate)
                    {
                        return _callbacks?.Count > 0;
                    }
                }
            }

            public IDisposable RegisterChangeCallback(Action<object> callback, object state)
            {
                if (state != null)
                {
                    throw new ArgumentException($"Stateful callbacks are not supported for {nameof(PollingFileChangeToken)}", nameof(state));
                }

                lock (_gate)
                {
                    if (_callbacks == null)
                    {
                        _callbacks = new Dictionary<int, Action<object>>();
                    }

                    var callbackId = _nextCallbackId++;
                    _callbacks.Add(callbackId, callback);

                    // Ensure that we're polling when we've got a callback.
                    if (_callbacks.Count == 1)
                    {
                        StartPolling();
                    }

                    return new DisposableChangeCallback(this, callbackId);
                }
            }

            private void UnregisterChangeCallback(int callbackId)
            {
                lock (_gate)
                {
                    if (_callbacks != null)
                    {
                        _callbacks.Remove(callbackId);

                        if (_callbacks.Count == 0)
                        {
                            // We don't have any callbacks, so stop polling.
                            StopPolling();
                        }
                    }
                }
            }
        }
    }
}