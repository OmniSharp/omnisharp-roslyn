using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring
{

    public class DocumentWithFixAll
    {
        private readonly DocumentDiagnostics _documentDiagnostics;

        // http://source.roslyn.io/#Microsoft.VisualStudio.LanguageServices.CSharp/LanguageService/CSharpCodeCleanupFixer.cs,d9a375db0f1e430e,references
        // CS8019 isn't directly used (via roslyn) but has an analyzer that report different diagnostic based on CS8019 to improve user experience.
        private static readonly Dictionary<string, string> _customDiagVsFixMap = new Dictionary<string, string>
        {
            { "CS8019", "RemoveUnnecessaryImportsFixable" }
        };

        private DocumentWithFixAll(CodeFixProvider provider, DocumentDiagnostics documentDiagnostics)
        {
            CodeFixProvider = provider;
            _documentDiagnostics = documentDiagnostics;
            FixAllProvider = provider.GetFixAllProvider() ?? WellKnownFixAllProviders.BatchFixer;
        }


        public CodeFixProvider CodeFixProvider { get; }
        public FixAllProvider FixAllProvider { get; }
        public DocumentId DocumentId => _documentDiagnostics.DocumentId;
        public ProjectId ProjectId => _documentDiagnostics.ProjectId;
        public string DocumentPath => _documentDiagnostics.DocumentPath;

        public ImmutableArray<(string id, string message)> GetAvailableFixableDiagnostics() => _documentDiagnostics.Diagnostics
                .Where(x => HasFix(CodeFixProvider, x.Id))
                .Select(x => (x.Id, x.GetMessage()))
                .ToImmutableArray();

        private static bool HasFix(CodeFixProvider codeFixProvider, string diagnosticId)
        {
            return codeFixProvider.FixableDiagnosticIds.Any(id => id == diagnosticId)
                || (_customDiagVsFixMap.ContainsKey(diagnosticId) && codeFixProvider.FixableDiagnosticIds.Any(id => id == _customDiagVsFixMap[diagnosticId]));
        }

        public static ImmutableArray<DocumentWithFixAll> CreateWithMatchingProviders(ImmutableArray<CodeFixProvider> providers, DocumentDiagnostics documentDiagnostics)
        {
            return
                providers
                    .Select(provider => new DocumentWithFixAll(provider, documentDiagnostics))
                    .Where(x => x.GetAvailableFixableDiagnostics().Any())
                    .ToImmutableArray();
        }
    }
}
