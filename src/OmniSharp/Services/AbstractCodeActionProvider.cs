#if DNX451
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Services
{
    public abstract class AbstractCodeActionProvider : ICodeActionProvider
    {
        private readonly IEnumerable<CodeRefactoringProvider> _refactorings;
        private readonly IEnumerable<CodeFixProvider> _codeFixes;

        protected AbstractCodeActionProvider(string assemblyName) : this(Assembly.Load(assemblyName))
        {
        }

        protected AbstractCodeActionProvider(Assembly codeActionAssembly)
        {
            var features = codeActionAssembly
                                .GetTypes()
                                .Where(type => !type.IsInterface
                                        && !type.IsAbstract
                                        && !type.ContainsGenericParameters); // TODO: handle providers with generic params

            _refactorings = features.Where(t => typeof(CodeRefactoringProvider).IsAssignableFrom(t))
                   .Select(type => (CodeRefactoringProvider)Activator.CreateInstance(type));

            _codeFixes = features.Where(t => typeof(CodeFixProvider).IsAssignableFrom(t))
                    .Select(type => (CodeFixProvider)Activator.CreateInstance(type));
        }

        public virtual IEnumerable<CodeRefactoringProvider> Refactorings => _refactorings;

        public virtual IEnumerable<CodeFixProvider> CodeFixes => _codeFixes;

        public abstract string ProviderName { get; }
    }
}
#endif
