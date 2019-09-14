using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Utilities;

namespace OmniSharp.Services
{
    public abstract class AbstractCodeActionProvider : ICodeActionProvider
    {
        public string ProviderName { get; }
        public ImmutableArray<CodeRefactoringProvider> CodeRefactoringProviders { get; }
        public ImmutableArray<CodeFixProvider> CodeFixProviders { get; }
        public ImmutableArray<DiagnosticAnalyzer> CodeDiagnosticAnalyzerProviders { get; }
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
                .Select(type => type.CreateInstance<CodeRefactoringProvider>())
                .Where(instance => instance != null)
                .ToImmutableArray();

            this.CodeFixProviders = types
                .Where(t => typeof(CodeFixProvider).IsAssignableFrom(t))
                .Select(type => type.CreateInstance<CodeFixProvider>())
                .Where(instance => instance != null)
                .ToImmutableArray();

            this.CodeDiagnosticAnalyzerProviders = types
                .Where(t => typeof(DiagnosticAnalyzer).IsAssignableFrom(t))
                .Select(type => type.CreateInstance<DiagnosticAnalyzer>())
                .Where(instance => instance != null)
                .ToImmutableArray();
        }
    }
}

