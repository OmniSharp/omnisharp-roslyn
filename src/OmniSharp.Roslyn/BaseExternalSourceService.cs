using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.MetadataAsSource;

namespace OmniSharp.Roslyn
{
    public abstract class BaseExternalSourceService
    {
        protected ConcurrentDictionary<string, Document> _cache = new ConcurrentDictionary<string, Document>();

        protected BaseExternalSourceService()
        {
        }

        public Document FindDocumentInCache(string fileName)
        {
            if (_cache.TryGetValue(fileName, out var metadataDocument))
            {
                return metadataDocument;
            }

            return null;
        }

        public Task<Location> GetExternalSymbolLocation(ISymbol symbol, Document metadataDocument, CancellationToken cancellationToken = new CancellationToken())
            => OmniSharpMetadataAsSourceHelpers.GetLocationInGeneratedSourceAsync(symbol, metadataDocument, cancellationToken);
    }
}
