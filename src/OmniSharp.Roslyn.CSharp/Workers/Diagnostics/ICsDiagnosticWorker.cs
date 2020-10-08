using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    public interface ICsDiagnosticWorker
    {
        Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<string> documentPaths);
        Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<Document> documents);
        Task<ImmutableArray<DocumentDiagnostics>> GetAllDiagnosticsAsync();
        ImmutableArray<Document> QueueDocumentsForDiagnostics();
        ImmutableArray<Document> QueueDocumentsForDiagnostics(ImmutableArray<ProjectId> projectId);
    }
}
