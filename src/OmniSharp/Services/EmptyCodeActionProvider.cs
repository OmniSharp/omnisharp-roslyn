using System.Collections.Generic;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Services
{
    public class EmptyCodeActionProvider : ICodeActionProvider
    {
        public IEnumerable<CodeRefactoringProvider> GetProviders()
        {
            return new List<CodeRefactoringProvider>();
        }
    }
}