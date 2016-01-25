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

        protected AbstractCodeActionProvider(string providerName,
                                             IOmnisharpAssemblyLoader loader,
                                             params string[] assembliesNames)
        {
            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            ProviderName = providerName;

            Assemblies = assembliesNames.Select(name => loader.Load(name));

            var features = Assemblies.SelectMany(assembly => assembly.GetTypes()
                                     .Where(type => !type.GetTypeInfo().IsInterface &&
                                                    !type.GetTypeInfo().IsAbstract &&
                                                    !type.GetTypeInfo().ContainsGenericParameters));
            // TODO: handle providers with generic params

            _refactorings = features.Where(t => typeof(CodeRefactoringProvider).IsAssignableFrom(t))
                                    .Select(type => CreateInstance<CodeRefactoringProvider>(type))
                                    .Where(instance => instance != null);

            _codeFixes = features.Where(t => typeof(CodeFixProvider).IsAssignableFrom(t))
                                 .Select(type => CreateInstance<CodeFixProvider>(type))
                                 .Where(instance => instance != null);
        }

        public virtual IEnumerable<CodeRefactoringProvider> Refactorings => _refactorings;

        public virtual IEnumerable<CodeFixProvider> CodeFixes => _codeFixes;

        public virtual IEnumerable<Assembly> Assemblies { get; protected set; }

        public string ProviderName { get; }

        private T CreateInstance<T>(Type type) where T : class
        {
            try
            {
                var defaultCtor = type.GetConstructor(new Type[] { });
                if (defaultCtor != null)
                {
                    return (T)Activator.CreateInstance(type);
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create instrance of {type.FullName} in {type.AssemblyQualifiedName}.", ex);
            }
        }
    }
}

