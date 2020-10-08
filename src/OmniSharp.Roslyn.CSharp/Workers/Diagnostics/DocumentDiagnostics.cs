using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    public class DocumentDiagnostics
    {
        public DocumentDiagnostics(Document document, ImmutableArray<Diagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
            Document = document;
        }

        public Document Document { get;  }
        public Project Project => Document.Project;
        public ImmutableArray<Diagnostic> Diagnostics { get; }
    }
}
