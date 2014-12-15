using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.Framework.Cache.Memory;
using Microsoft.Framework.Logging;

namespace OmniSharp.Services
{
    public class MetadataFileReferenceCache : IMetadataFileReferenceCache
    {
        private static readonly string _cacheKeyPrefix = nameof(MetadataFileReferenceCache);

        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;

        public MetadataFileReferenceCache(IMemoryCache cache, ILoggerFactory loggerFactory)
        {
            _cache = cache;
            _logger = loggerFactory.Create<MetadataFileReferenceCache>();
        }

        public MetadataReference GetMetadataReference(string path)
        {
            var cacheKey = _cacheKeyPrefix + path.ToLowerInvariant();

            return _cache.GetOrSet(cacheKey, ctx =>
            {
                _logger.WriteVerbose(string.Format("Cache miss {0}", path));

                ctx.AddExpirationTrigger(new FileWriteTimeTrigger(path));

                return MetadataReference.CreateFromFile(path);
            });
        }

        private class FileWriteTimeTrigger : IExpirationTrigger
        {
            private readonly string _path;
            private readonly DateTime _lastWriteTime;
            public FileWriteTimeTrigger(string path)
            {
                _path = path;
                _lastWriteTime = File.GetLastWriteTime(path).ToUniversalTime();
            }

            public bool ActiveExpirationCallbacks
            {
                get
                {
                    return false;
                }
            }

            public bool IsExpired
            {
                get
                {
                    return File.GetLastWriteTime(_path).ToUniversalTime() > _lastWriteTime;
                }
            }

            public IDisposable RegisterExpirationCallback(Action<object> callback, object state)
            {
                throw new NotImplementedException();
            }
        }
    }
}