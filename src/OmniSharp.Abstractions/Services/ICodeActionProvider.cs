using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Services
{
    public interface ICodeActionProvider
    {
        IEnumerable<CodeRefactoringProvider> Refactorings { get; }
        IEnumerable<CodeFixProvider> CodeFixes { get; }
        IEnumerable<Assembly> Assemblies { get; }
    }
}
