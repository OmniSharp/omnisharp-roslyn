using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Services
{
    public abstract class AbstractCodeActionProvider : ICodeActionProvider
    {
        private readonly bool _throwOnException;
        private readonly ILogger _logger;
        public string ProviderName { get; }
        public ImmutableArray<CodeRefactoringProvider> Refactorings { get; }
        public ImmutableArray<CodeFixProvider> CodeFixes { get; }

        public ImmutableArray<Assembly> Assemblies { get; }

        protected AbstractCodeActionProvider(
            ILoggerFactory loggerFactory,
            string providerName,
            ImmutableArray<Assembly> assemblies,
            bool throwOnException) : this(loggerFactory, providerName, assemblies)
        {
            _throwOnException = throwOnException;
        }

        protected AbstractCodeActionProvider(
            ILoggerFactory loggerFactory,
            string providerName,
            ImmutableArray<Assembly> assemblies)
        {
            if (providerName.EndsWith("CodeActionProvider"))
            {
                providerName = providerName.Replace("CodeActionProvider", "");
            }
            _logger = loggerFactory.CreateLogger(providerName);
            ProviderName = providerName;

            this.Assemblies = assemblies;

            var types = this.Assemblies
                .SelectMany(assembly => assembly.GetTypes()
                .Where(type => !type.GetTypeInfo().IsInterface &&
                               !type.GetTypeInfo().IsAbstract &&
                               !type.GetTypeInfo().ContainsGenericParameters));
            // TODO: handle providers with generic params

            this.Refactorings = types
                .Where(t => typeof(CodeRefactoringProvider).IsAssignableFrom(t))
                .Select(type => CreateInstance<CodeRefactoringProvider>(type))
                .Where(instance => instance != null)
                .ToImmutableArray();

            this.CodeFixes = types
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
                if (this._throwOnException)
                {
                    throw new InvalidOperationException($"Failed to create instance of {type.FullName} in {type.AssemblyQualifiedName}.", ex);
                }
                else
                {
                    _logger.LogError($"Failed to create instance of {type.FullName} in {type.AssemblyQualifiedName}.\n{ex.ToString()}");
                    return null;
                }
            }
        }
    }
}
