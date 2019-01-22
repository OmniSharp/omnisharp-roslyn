using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    public interface ICsDiagnosticWorker
    {
        Task<ImmutableArray<(string projectName, Diagnostic diagnostic)>> GetDiagnostics(ImmutableArray<Document> documents);
        void QueueForDiagnosis(ImmutableArray<Document> documents);
    }
}