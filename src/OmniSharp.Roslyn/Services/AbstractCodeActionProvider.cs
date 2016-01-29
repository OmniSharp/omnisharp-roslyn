using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.Extensions.PlatformAbstractions;

namespace OmniSharp.Services
{
    public abstract class AbstractCodeActionProvider : ICodeActionProvider
    {
        private readonly IEnumerable<CodeRefactoringProvider> _refactorings;
        private readonly IEnumerable<CodeFixProvider> _codeFixes;

        protected AbstractCodeActionProvider(string assemblyName) : this(DnxPlatformServices.Default.AssemblyLoadContextAccessor.Default.Load(assemblyName))
        {
        }

        protected AbstractCodeActionProvider(params Assembly[] codeActionAssemblies)
        {
            var features = codeActionAssemblies
                                .SelectMany(assembly => assembly.GetTypes()
                                .Where(type => !type.GetTypeInfo().IsInterface
                                        && !type.GetTypeInfo().IsAbstract
                                        && !type.GetTypeInfo().ContainsGenericParameters)); // TODO: handle providers with generic params

            _refactorings = features.Where(t => typeof(CodeRefactoringProvider).IsAssignableFrom(t))
                   .Select(type => (CodeRefactoringProvider)Activator.CreateInstance(type));

            _codeFixes = features.Where(t => typeof(CodeFixProvider).IsAssignableFrom(t))
                    .Select(type => (CodeFixProvider)Activator.CreateInstance(type));

            Assemblies = codeActionAssemblies;
        }

        public virtual IEnumerable<CodeRefactoringProvider> Refactorings => _refactorings;

        public virtual IEnumerable<CodeFixProvider> CodeFixes => _codeFixes;

        public virtual IEnumerable<Assembly> Assemblies { get; protected set; }

        public abstract string ProviderName { get; }
    }
}

