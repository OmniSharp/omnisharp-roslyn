using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Services
{
    public interface ICodeActionProvider
    {
        ImmutableArray<CodeRefactoringProvider> CodeRefactoringProviders { get; }
        ImmutableArray<CodeFixProvider> CodeFixProviders { get; }
        ImmutableArray<Assembly> Assemblies { get; }
    }
}
