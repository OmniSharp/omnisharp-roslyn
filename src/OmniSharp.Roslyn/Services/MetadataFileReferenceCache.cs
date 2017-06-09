using System;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace OmniSharp.Services
{
    [Export, Shared]
    public class MetadataFileReferenceCache
    {
        private static readonly string _cacheKeyPrefix = nameof(MetadataFileReferenceCache);

        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public MetadataFileReferenceCache(IMemoryCache cache, ILoggerFactory loggerFactory)
        {
            _cache = cache;
            _logger = loggerFactory.CreateLogger<MetadataFileReferenceCache>();
        }

        public MetadataReference GetMetadataReference(string filePath)
        {
            var cacheKey = _cacheKeyPrefix + filePath.ToLowerInvariant();

            var assemblyMetadata = _cache.Get<AssemblyMetadata>(cacheKey);
            if (assemblyMetadata == null)
            {
                _logger.LogDebug(string.Format("Cache miss {0}", filePath));

                using (var stream = File.OpenRead(filePath))
                {
                    var moduleMetadata = ModuleMetadata.CreateFromStream(stream, PEStreamOptions.PrefetchMetadata);
                    assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);

                    var options = new MemoryCacheEntryOptions();
                    options.ExpirationTokens.Add(new FileWriteTimeTrigger(filePath));

                    _cache.Set(cacheKey, assemblyMetadata, options);
                }
            }

            var displayText = assemblyMetadata.GetModules().FirstOrDefault()?.Name;

            var documentationFile = Path.ChangeExtension(filePath, ".xml");
            var documentationProvider = File.Exists(documentationFile)
                ? XmlDocumentationProvider.CreateFromFile(documentationFile)
                : null;

            return assemblyMetadata.GetReference(
                documentationProvider, filePath: filePath, display: displayText);
        }

        private class FileWriteTimeTrigger : IChangeToken
        {
            private readonly string _path;
            private readonly DateTime _lastWriteTime;
            public FileWriteTimeTrigger(string path)
            {
                _path = path;
                _lastWriteTime = File.GetLastWriteTime(path).ToUniversalTime();
            }

            public bool ActiveChangeCallbacks
            {
                get
                {
                    return false;
                }
            }

            public bool HasChanged
            {
                get
                {
                    return File.GetLastWriteTime(_path).ToUniversalTime() > _lastWriteTime;
                }
            }

            public IDisposable RegisterChangeCallback(Action<object> callback, object state)
            {
                throw new NotImplementedException();
            }
        }
    }
}
