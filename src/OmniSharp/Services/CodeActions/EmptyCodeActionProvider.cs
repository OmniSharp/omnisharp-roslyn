using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Services
{
    public class EmptyCodeActionProvider : ICodeActionProvider
    {
        public IEnumerable<CodeRefactoringProvider> Refactorings => Enumerable.Empty<CodeRefactoringProvider>();
        public IEnumerable<CodeFixProvider> CodeFixes => Enumerable.Empty<CodeFixProvider>();
    }
}
