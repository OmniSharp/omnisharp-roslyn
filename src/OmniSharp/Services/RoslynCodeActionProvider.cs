#if DNX451
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Services
{
    public class RoslynCodeActionProvider : ICodeActionProvider
    {
        private readonly IEnumerable<CodeRefactoringProvider> _refactorings;
        private readonly IEnumerable<CodeFixProvider> _codeFixes;

        public RoslynCodeActionProvider()
        {
            var features = Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35")
                                .GetTypes()
                                .Where(type => !type.IsInterface
                                        && !type.IsAbstract
                                        && !type.ContainsGenericParameters); // TODO: handle providers with generic params

            _refactorings = features.Where(t => typeof(CodeRefactoringProvider).IsAssignableFrom(t))
                   .Select(type => (CodeRefactoringProvider)Activator.CreateInstance(type));

            _codeFixes = features.Where(t => typeof(CodeFixProvider).IsAssignableFrom(t))
                    .Select(type => (CodeFixProvider)Activator.CreateInstance(type));
        }

        public IEnumerable<CodeRefactoringProvider> Refactorings => _refactorings;

        public IEnumerable<CodeFixProvider> CodeFixes => _codeFixes;
    }
}
#endif
