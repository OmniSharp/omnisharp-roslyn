using System;
using System.IO;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.Framework.Caching.Memory;
using Microsoft.Framework.Caching.Distributed;
using Microsoft.Framework.Logging;
using OmniSharp.Roslyn;

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
            _logger = loggerFactory.CreateLogger<MetadataFileReferenceCache>();
        }

        public MetadataReference GetMetadataReference(string path)
        {
            var cacheKey = _cacheKeyPrefix + path.ToLowerInvariant();

            var metadata = _cache.Get<AssemblyMetadata>(cacheKey);

            if (metadata == null)
            {
                _logger.LogVerbose(string.Format("Cache miss {0}", path));

                // HOWTODO? ctx.AddExpirationTrigger(new FileWriteTimeTrigger(path));

                using (var stream = File.OpenRead(path))
                {
                    var moduleMetadata = ModuleMetadata.CreateFromStream(stream, PEStreamOptions.PrefetchMetadata);
                    metadata = AssemblyMetadata.Create(moduleMetadata);
                    _cache.Set(cacheKey, metadata);
                }
            }

            var documentationFile = Path.ChangeExtension(path, ".xml");
            if (File.Exists(documentationFile))
            {
                return metadata.GetReference(new XmlDocumentationProvider(documentationFile));
            }

            return metadata.GetReference();
        }
/*
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
*/
    }
}
