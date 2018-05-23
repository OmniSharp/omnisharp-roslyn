using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dotnet.Script.DependencyModel.Compilation;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Services;

namespace OmniSharp.Script
{
    public class ScriptContext
    {
        private readonly ScriptOptions _scriptOptions;
        private readonly CompilationDependencyResolver _compilationDependencyResolver;
        private readonly IOmniSharpEnvironment _env;
        private readonly MetadataFileReferenceCache _metadataFileReferenceCache;
        private readonly ILogger _logger;

        public ScriptContext(ScriptOptions scriptOptions, CompilationDependencyResolver compilationDependencyResolver, ILoggerFactory loggerFactory, IOmniSharpEnvironment env, MetadataFileReferenceCache metadataFileReferenceCache)
        {
            _scriptOptions = scriptOptions;
            _compilationDependencyResolver = compilationDependencyResolver;
            _env = env;
            _metadataFileReferenceCache = metadataFileReferenceCache;
            _logger = loggerFactory.CreateLogger<ScriptContext>();

            ScriptOptions = scriptOptions;

            var currentDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            // explicitly inherit scripting library references to all global script object (CommandLineScriptGlobals) to be recognized
            var inheritedCompileLibraries = currentDomainAssemblies.Where(x =>
                x.FullName.StartsWith("microsoft.codeanalysis", StringComparison.OrdinalIgnoreCase)).ToList();

            // explicitly include System.ValueTuple
            inheritedCompileLibraries.AddRange(currentDomainAssemblies.Where(x =>
                x.FullName.StartsWith("system.valuetuple", StringComparison.OrdinalIgnoreCase)));

            CompilationDependencies = TryGetCompilationDependencies();

            var isDesktopClr = true;
            // if we have no compilation dependencies
            // we will assume desktop framework
            // and add default CLR references
            // same applies for having a context that is not a .NET Core app
            if (!CompilationDependencies.Any())
            {
                _logger.LogInformation("Unable to find dependency context for CSX files. Will default to non-context usage (Desktop CLR scripts).");
                AddDefaultClrMetadataReferences(MetadataReferences);
            }
            else
            {
                isDesktopClr = false;
                HashSet<string> loadedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var compilationAssembly in CompilationDependencies.SelectMany(cd => cd.AssemblyPaths).Distinct())
                {
                    if (loadedFiles.Add(Path.GetFileName(compilationAssembly)))
                    {
                        _logger.LogDebug("Discovered script compilation assembly reference: " + compilationAssembly);
                        AddMetadataReference(MetadataReferences, compilationAssembly);
                    }
                }
            }

            // inject all inherited assemblies
            foreach (var inheritedCompileLib in inheritedCompileLibraries)
            {
                _logger.LogDebug("Adding implicit reference: " + inheritedCompileLib);
                AddMetadataReference(MetadataReferences, inheritedCompileLib.Location);
            }

            ScriptProjectProvider = new ScriptProjectProvider(_scriptOptions, _env, loggerFactory, isDesktopClr);
        }

        public ScriptOptions ScriptOptions { get; }

        public ScriptProjectProvider ScriptProjectProvider { get; }

        public HashSet<MetadataReference> MetadataReferences { get; } = new HashSet<MetadataReference>(MetadataReferenceEqualityComparer.Instance);

        public HashSet<string> AssemblyReferences = new HashSet<string>();

        public CompilationDependency[] CompilationDependencies { get; }

        private CompilationDependency[] TryGetCompilationDependencies()
        {
            try
            {
                _logger.LogInformation($"Searching for compilation dependencies with the fallback framework of '{_scriptOptions.DefaultTargetFramework}'.");
                return _compilationDependencyResolver.GetDependencies(_env.TargetDirectory, _scriptOptions.IsNugetEnabled(), _scriptOptions.DefaultTargetFramework).ToArray();
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to resolve compilation dependencies", e);
                return Array.Empty<CompilationDependency>();
            }
        }

        private void AddDefaultClrMetadataReferences(HashSet<MetadataReference> commonReferences)
        {
            var assemblies = new[]
            {
                typeof(object).GetTypeInfo().Assembly,
                typeof(Enumerable).GetTypeInfo().Assembly,
                typeof(Stack<>).GetTypeInfo().Assembly,
                typeof(Lazy<,>).GetTypeInfo().Assembly,
                FromName("System.Runtime"),
                FromName("mscorlib")
            };

            var references = assemblies
                .Where(a => a != null)
                .Select(a => a.Location)
                .Distinct()
                .Select(l =>
                {
                    AssemblyReferences.Add(l);
                    return _metadataFileReferenceCache.GetMetadataReference(l);
                });

            foreach (var reference in references)
            {
                commonReferences.Add(reference);
            }

            Assembly FromName(string assemblyName)
            {
                try
                {
                    return Assembly.Load(new AssemblyName(assemblyName));
                }
                catch
                {
                    return null;
                }
            }
        }

        private void AddMetadataReference(ISet<MetadataReference> referenceCollection, string fileReference)
        {
            if (!File.Exists(fileReference))
            {
                _logger.LogWarning($"Couldn't add reference to '{fileReference}' because the file was not found.");
                return;
            }

            var metadataReference = _metadataFileReferenceCache.GetMetadataReference(fileReference);
            if (metadataReference == null)
            {
                _logger.LogWarning($"Couldn't add reference to '{fileReference}' because the loaded metadata reference was null.");
                return;
            }

            referenceCollection.Add(metadataReference);
            AssemblyReferences.Add(fileReference);
            _logger.LogDebug($"Added reference to '{fileReference}'");
        }
    }
}
