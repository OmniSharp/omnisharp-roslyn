using System;
using System.Collections.Generic;
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

        protected AbstractCodeActionProvider(string providerName, params Assembly[] codeActionAssemblies)
        {
            ProviderName = providerName;

            Assemblies = codeActionAssemblies;

            var features = codeActionAssemblies
                .SelectMany(assembly => assembly.GetTypes()
                .Where(type => !type.GetTypeInfo().IsInterface &&
                               !type.GetTypeInfo().IsAbstract &&
                               !type.GetTypeInfo().ContainsGenericParameters));
            // TODO: handle providers with generic params

            _refactorings = CreateInstances<CodeRefactoringProvider>(features);
            _codeFixes = CreateInstances<CodeFixProvider>(features);

            /*
            foreach (var refactoring in _refactorings) {
                Console.WriteLine(refactoring.GetType().FullName);
            }
            */
        }

        private static IEnumerable<T> CreateInstances<T>(IEnumerable<Type> types)
        {
            var targetType = typeof(T);
            foreach (var type in types)
            {
                if (targetType.IsAssignableFrom(type))
                {
                    yield return (T)Activator.CreateInstance(type);
                }
            }
        }

        public virtual IEnumerable<CodeRefactoringProvider> Refactorings => _refactorings;

        public virtual IEnumerable<CodeFixProvider> CodeFixes => _codeFixes;

        public IEnumerable<Assembly> Assemblies { get; }

        public string ProviderName { get; }
    }
}
