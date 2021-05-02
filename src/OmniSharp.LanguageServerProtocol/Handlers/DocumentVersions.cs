using System.Collections.Concurrent;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    public class DocumentVersions
    {
        private readonly ConcurrentDictionary<DocumentUri, int> _documentVersions = new ConcurrentDictionary<DocumentUri, int>();

        public int? GetVersion(DocumentUri documentUri)
        {
            if (_documentVersions.TryGetValue(documentUri, out var version))
            {
                return version;
            }

            return null;
        }

        public void Update(VersionedTextDocumentIdentifier identifier)
        {
            _documentVersions.AddOrUpdate(identifier.Uri, identifier.Version, (uri, i) => identifier.Version);
        }

        public void Update(OptionalVersionedTextDocumentIdentifier identifier)
        {
            _documentVersions.AddOrUpdate(identifier.Uri, identifier.Version ?? 0, (uri, i) => identifier.Version ?? 0);
        }

        public void Reset(TextDocumentIdentifier identifier)
        {
            _documentVersions.AddOrUpdate(identifier.Uri, 0, (uri, i) => 0);
        }

        public void Remove(TextDocumentIdentifier identifier)
        {
            _documentVersions.TryRemove(identifier.Uri, out _);
        }
    }
}
