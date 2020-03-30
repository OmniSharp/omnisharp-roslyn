using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn
{
    public interface IExternalSourceService
    {
        Task<(Document metadataDocument, string documentPath)> GetAndAddExternalSymbolDocument(Project project, ISymbol symbol, CancellationToken cancellationToken);
        Document FindDocumentInCache(string fileName);
        Task<Location> GetExternalSymbolLocation(ISymbol symbol, Document metadataDocument, CancellationToken cancellationToken);
        string GetSymbolName(ISymbol symbol);
    }
}
