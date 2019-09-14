using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace OmniSharp.Utilities
{
    public static partial class PhysicalFileProviderExtensions
    {
        private class PhysicalFileProviderWrapper : DisposableObject, IFileProvider
        {
            private readonly string _root;
            private readonly PhysicalFileProvider _innerFileProvider;

            private Dictionary<string, PollingFileChangeToken> _changeTokens;
            private readonly object _gate = new object();

            public PhysicalFileProviderWrapper(PhysicalFileProvider fileProvider)
            {
                _root = fileProvider.Root;
                _innerFileProvider = fileProvider;
            }

            protected override void DisposeCore(bool disposing)
            {
                foreach (var kvp in _changeTokens)
                {
                    kvp.Value.Dispose();
                }

                _changeTokens.Clear();
                _changeTokens = null;

                _innerFileProvider.Dispose();
            }

            public IDirectoryContents GetDirectoryContents(string subpath)
            {
                return _innerFileProvider.GetDirectoryContents(subpath);
            }

            public IFileInfo GetFileInfo(string subpath)
            {
                return _innerFileProvider.GetFileInfo(subpath);
            }

            public IChangeToken Watch(string filter)
            {
                if (filter.IndexOf('*') >= 0)
                {
                    throw new ArgumentException($"Wildcards are not allowed", nameof(filter));
                }

                var filePath = Path.Combine(_root, filter);

                lock (_gate)
                {
                    if (_changeTokens == null)
                    {
                        _changeTokens = new Dictionary<string, PollingFileChangeToken>(StringComparer.OrdinalIgnoreCase);
                    }

                    if (!_changeTokens.TryGetValue(filePath, out var changeToken))
                    {
                        changeToken = new PollingFileChangeToken(filePath);
                        _changeTokens.Add(filePath, changeToken);
                    }

                    return changeToken;
                }
            }
        }
    }
}
