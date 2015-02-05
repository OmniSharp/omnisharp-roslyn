using System.Collections.Generic;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Services
{
    public interface ICodeActionProvider
    {
        IEnumerable<CodeRefactoringProvider> GetProviders();
    }
}