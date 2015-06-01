using System.Collections.Generic;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Services
{
    public interface ICodeActionProvider
    {
        IEnumerable<CodeRefactoringProvider> GetRefactorings();
        IEnumerable<CodeFixProvider> GetCodeFixes();
    }
}
