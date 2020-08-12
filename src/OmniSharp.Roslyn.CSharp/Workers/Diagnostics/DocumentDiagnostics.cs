using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    public class DocumentDiagnostics
    {
        public DocumentDiagnostics(DocumentId documentId, string documentPath, ProjectId projectId, string projectName, ImmutableArray<Diagnostic> diagnostics)
        {
            DocumentId = documentId;
            DocumentPath = documentPath;
            ProjectId = projectId;
            ProjectName = projectName;
            Diagnostics = diagnostics;
        }

        public DocumentId DocumentId { get; }
        public ProjectId ProjectId { get; }
        public string ProjectName { get; }
        public string DocumentPath { get; }
        public ImmutableArray<Diagnostic> Diagnostics { get; }
    }
}
