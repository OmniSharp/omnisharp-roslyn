using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace OmniSharp.Services
{
    public abstract class AbstractCodeActionProvider : ICodeActionProvider
    {
        public string ProviderName { get; }
        public ImmutableArray<CodeRefactoringProvider> CodeRefactoringProviders { get; }
        public ImmutableArray<CodeFixProvider> CodeFixProviders { get; }

        public ImmutableArray<Assembly> Assemblies { get; }

        protected AbstractCodeActionProvider(string providerName, ImmutableArray<Assembly> assemblies)
        {
            ProviderName = providerName;

            this.Assemblies = assemblies;

            var types = this.Assemblies
                .SelectMany(assembly => assembly.GetTypes()
                .Where(type => !type.GetTypeInfo().IsInterface &&
                               !type.GetTypeInfo().IsAbstract &&
                               !type.GetTypeInfo().ContainsGenericParameters));
            // TODO: handle providers with generic params

            this.CodeRefactoringProviders = types
                .Where(t => typeof(CodeRefactoringProvider).IsAssignableFrom(t))
                .Select(type => CreateInstance<CodeRefactoringProvider>(type))
                .Where(instance => instance != null)
                .ToImmutableArray();

            this.CodeFixProviders = types
                .Where(t => typeof(CodeFixProvider).IsAssignableFrom(t))
                .Select(type => CreateInstance<CodeFixProvider>(type))
                .Where(instance => instance != null)
                .ToImmutableArray();
        }

        private T CreateInstance<T>(Type type) where T : class
        {
            try
            {
                var defaultCtor = type.GetConstructor(new Type[] { });

                return defaultCtor != null
                    ? (T)Activator.CreateInstance(type)
                    : null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create instrance of {type.FullName} in {type.AssemblyQualifiedName}.", ex);
            }
        }
    }
}

