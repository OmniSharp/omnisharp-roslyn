﻿using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using Dotnet.Script.DependencyModel.Compilation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.Extensions.Logging;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Services;
using LogLevel = Dotnet.Script.DependencyModel.Logging.LogLevel;

namespace OmniSharp.Script
{
    [Export, Shared]
    public class ScriptContextProvider
    {
        // default, which also force loads the Scripting DLL into the AppDomain
        private readonly Type _defaultGlobalsType = typeof(CommandLineScriptGlobals);
        private readonly ILoggerFactory _loggerFactory;
        private readonly CompilationDependencyResolver _compilationDependencyResolver;
        private readonly IOmniSharpEnvironment _env;
        private readonly MetadataFileReferenceCache _metadataFileReferenceCache;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public ScriptContextProvider(ILoggerFactory loggerFactory, IOmniSharpEnvironment env, MetadataFileReferenceCache metadataFileReferenceCache)
        {
            _loggerFactory = loggerFactory;
            _env = env;
            _metadataFileReferenceCache = metadataFileReferenceCache;
            _logger = loggerFactory.CreateLogger<ScriptContextProvider>();
            _compilationDependencyResolver = new CompilationDependencyResolver(type =>
            {
                // Prefix with "OmniSharp" so that we make it through the log filter.
                var categoryName = $"OmniSharp.Script.{type.FullName}";
                var dependencyResolverLogger = loggerFactory.CreateLogger(categoryName);
                return ((level, message) =>
                {
                    if (level == LogLevel.Debug)
                    {
                        dependencyResolverLogger.LogDebug(message);
                    }
                    if (level == LogLevel.Info)
                    {
                        dependencyResolverLogger.LogInformation(message);
                    }
                });
            });
        }

        public ScriptContext CreateScriptContext(ScriptOptions scriptOptions)
        {
            var currentDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            // explicitly inherit scripting library references to all global script object (CommandLineScriptGlobals) to be recognized
            var inheritedCompileLibraries = currentDomainAssemblies.Where(x =>
                x.FullName.StartsWith("microsoft.codeanalysis", StringComparison.OrdinalIgnoreCase)).ToList();

            // explicitly include System.ValueTuple
            inheritedCompileLibraries.AddRange(currentDomainAssemblies.Where(x =>
                x.FullName.StartsWith("system.valuetuple", StringComparison.OrdinalIgnoreCase)));

            CompilationDependency[] compilationDependencies = null;
            try
            {
                _logger.LogInformation($"Searching for compilation dependencies with the fallback framework of '{scriptOptions.DefaultTargetFramework}'.");
                compilationDependencies = _compilationDependencyResolver.GetDependencies(_env.TargetDirectory, scriptOptions.IsNugetEnabled(), scriptOptions.DefaultTargetFramework).ToArray();
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to resolve compilation dependencies", e);
                compilationDependencies = Array.Empty<CompilationDependency>();
            }

            var metadataReferences = new HashSet<MetadataReference>(MetadataReferenceEqualityComparer.Instance);
            var assemblyReferences = new HashSet<string>();

            var isDesktopClr = true;
            // if we have no compilation dependencies
            // we will assume desktop framework
            // and add default CLR references
            // same applies for having a context that is not a .NET Core app
            if (!compilationDependencies.Any())
            {
                _logger.LogInformation("Unable to find dependency context for CSX files. Will default to non-context usage (Desktop CLR scripts).");
                AddDefaultClrMetadataReferences(metadataReferences, assemblyReferences);
            }
            else
            {
                isDesktopClr = false;
                HashSet<string> loadedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var compilationAssembly in compilationDependencies.SelectMany(cd => cd.AssemblyPaths).Distinct())
                {
                    if (loadedFiles.Add(Path.GetFileName(compilationAssembly)))
                    {
                        _logger.LogDebug("Discovered script compilation assembly reference: " + compilationAssembly);
                        AddMetadataReference(metadataReferences, assemblyReferences, compilationAssembly);
                    }
                }
            }

            // inject all inherited assemblies
            foreach (var inheritedCompileLib in inheritedCompileLibraries)
            {
                _logger.LogDebug("Adding implicit reference: " + inheritedCompileLib);
                AddMetadataReference(metadataReferences, assemblyReferences, inheritedCompileLib.Location);
            }

            var scriptProjectProvider = new ScriptProjectProvider(scriptOptions, _env, _loggerFactory, isDesktopClr);

            return new ScriptContext(scriptProjectProvider, metadataReferences, assemblyReferences, compilationDependencies, _defaultGlobalsType);
        }

        private void AddDefaultClrMetadataReferences(HashSet<MetadataReference> commonReferences, HashSet<string> assemblyReferences)
        {
            var references = DefaultMetadataReferenceHelper.GetDefaultMetadataReferenceLocations()
                .Select(l =>
                {
                    assemblyReferences.Add(l);
                    return _metadataFileReferenceCache.GetMetadataReference(l);
                });

            foreach (var reference in references)
            {
                commonReferences.Add(reference);
            }
        }

        private void AddMetadataReference(ISet<MetadataReference> referenceCollection, HashSet<string> assemblyReferences, string fileReference)
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
            assemblyReferences.Add(fileReference);
            _logger.LogDebug($"Added reference to '{fileReference}'");
        }
    }
}
