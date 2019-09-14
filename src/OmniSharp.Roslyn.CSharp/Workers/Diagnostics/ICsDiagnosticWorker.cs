using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    public interface ICsDiagnosticWorker
    {
        Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetDiagnostics(ImmutableArray<string> documentPaths);
        Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetAllDiagnosticsAsync();
        ImmutableArray<DocumentId> QueueDocumentsForDiagnostics();
        ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(ImmutableArray<ProjectId> projectId);
    }
}