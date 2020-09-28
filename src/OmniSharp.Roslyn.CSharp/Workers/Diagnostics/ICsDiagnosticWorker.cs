using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    public interface ICsDiagnosticWorker
    {
        Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<string> documentPaths);
        Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<Document> documents, bool skipCache);
        Task<ImmutableArray<DocumentDiagnostics>> GetAllDiagnosticsAsync();
        ImmutableArray<DocumentId> QueueDocumentsForDiagnostics();
        ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(ImmutableArray<ProjectId> projectId);
    }
}
