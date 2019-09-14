using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OmniSharp.Services
{
    public interface ICodeActionProvider
    {
        ImmutableArray<CodeRefactoringProvider> CodeRefactoringProviders { get; }
        ImmutableArray<CodeFixProvider> CodeFixProviders { get; }
        ImmutableArray<DiagnosticAnalyzer> CodeDiagnosticAnalyzerProviders { get; }
        ImmutableArray<Assembly> Assemblies { get; }
    }
}
