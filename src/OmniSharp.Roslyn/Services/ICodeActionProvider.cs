using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Services
{
    public interface ICodeActionProvider
    {
        ImmutableArray<CodeRefactoringProvider> Refactorings { get; }
        ImmutableArray<CodeFixProvider> CodeFixes { get; }
        ImmutableArray<Assembly> Assemblies { get; }
    }
}
