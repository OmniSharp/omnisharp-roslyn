using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Dotnet.Script.DependencyModel.NuGet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.Extensions.Logging;
using OmniSharp.Helpers;
using OmniSharp.Roslyn.EditorConfig;
using OmniSharp.Roslyn.Utilities;

namespace OmniSharp.Script
{
    public class ScriptProjectProvider
    {
        private const string BinderFlagsType = "Microsoft.CodeAnalysis.CSharp.BinderFlags";
        private const string TopLevelBinderFlagsProperty = "TopLevelBinderFlags";
        private const string ReferencesSupersedeLowerVersionsProperty = "ReferencesSupersedeLowerVersions_internal_protected_set";
        private const string IgnoreCorLibraryDuplicatedTypesField = "IgnoreCorLibraryDuplicatedTypes";
        private const string RuntimeMetadataReferenceResolverType = "Microsoft.CodeAnalysis.Scripting.Hosting.RuntimeMetadataReferenceResolver";
        private const string ResolverField = "_resolver";
        private const string FileReferenceProviderField = "_fileReferenceProvider";

        // aligned with CSI.exe
        // https://github.com/dotnet/roslyn/blob/version-3.2.0/src/Interactive/csi/csi.desktop.rsp
        internal static readonly IEnumerable<string> DefaultNamespaces = new[]
        {
            "System",
            "System.IO",
            "System.Collections.Generic",
            "System.Console",
            "System.Diagnostics",
            "System.Dynamic",
            "System.Linq",
            "System.Linq.Expressions",
            "System.Text",
            "System.Threading.Tasks"
        };

        private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse, SourceCodeKind.Script);

        private readonly Lazy<CSharpCompilationOptions> _compilationOptions;
        private readonly Lazy<CSharpCommandLineArguments> _commandLineArgs;
        private readonly ScriptOptions _scriptOptions;
        private readonly IOmniSharpEnvironment _env;
        private readonly ILogger _logger;
        private readonly bool _isDesktopClr;
        private readonly bool _editorConfigEnabled;

        public ScriptProjectProvider(ScriptOptions scriptOptions, IOmniSharpEnvironment env, ILoggerFactory loggerFactory, bool isDesktopClr, bool editorConfigEnabled)
        {
            _scriptOptions = scriptOptions ?? throw new ArgumentNullException(nameof(scriptOptions));
            _env = env ?? throw new ArgumentNullException(nameof(env));

            _logger = loggerFactory.CreateLogger<ScriptProjectSystem>();
            _compilationOptions = new Lazy<CSharpCompilationOptions>(CreateCompilationOptions);
            _commandLineArgs = new Lazy<CSharpCommandLineArguments>(CreateCommandLineArguments);
            _isDesktopClr = isDesktopClr;
            _editorConfigEnabled = editorConfigEnabled;
        }

        private CSharpCommandLineArguments CreateCommandLineArguments()
        {
            if (!string.IsNullOrWhiteSpace(_scriptOptions.RspFilePath))
            {
                var rspFilePath = _scriptOptions.GetNormalizedRspFilePath(_env);
                if (rspFilePath != null)
                {
                    if(!File.Exists(rspFilePath))
                    {
                        _logger.LogError($"Unable to find RSP file at '{rspFilePath}` at path. Falling back on default values.");
                        return null;
                    }

                    _logger.LogInformation($"Discovered an RSP file at '{rspFilePath}' - will use this file to discover CSX namespaces and references.");
                    return CSharpCommandLineParser.Script.Parse(new string[] { $"@{rspFilePath}" },
                        _env.TargetDirectory,
                        _isDesktopClr ? Path.GetDirectoryName(typeof(object).GetTypeInfo().Assembly.ManifestModule.FullyQualifiedName) : null);
                }
            }

            return null;
        }

        private CSharpCompilationOptions CreateCompilationOptions()
        {
            var csharpCommandLineArguments = _commandLineArgs.Value;

            // if RSP file was used, pick namespaces from there
            // otherwise use default set of namespaces
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                usings: csharpCommandLineArguments != null
                    ? csharpCommandLineArguments.CompilationOptions.Usings
                    : DefaultNamespaces);

            foreach (var ns in compilationOptions.Usings)
            {
                _logger.LogDebug($"CSX global using statement: {ns}");
            }

            if(csharpCommandLineArguments != null)
            {
                foreach(var error in csharpCommandLineArguments.Errors)
                {
                    _logger.LogError($"CSX RSP parse error. {error.GetMessage()}");
                }
            }

            var metadataReferenceResolver = CreateMetadataReferenceResolver(csharpCommandLineArguments?.ReferencePaths);
            var sourceResolver = CreateScriptSourceResolver(csharpCommandLineArguments?.SourcePaths);

            compilationOptions = compilationOptions
                .WithAllowUnsafe(true)
                .WithMetadataReferenceResolver(metadataReferenceResolver)
                .WithSourceReferenceResolver(sourceResolver)
                .WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default)
                .WithSpecificDiagnosticOptions(!_scriptOptions.IsNugetEnabled()
                    ? CompilationOptionsHelper.GetDefaultSuppressedDiagnosticOptions()
                    : CompilationOptionsHelper.GetDefaultSuppressedDiagnosticOptions(_scriptOptions.NullableDiagnostics)); // for .NET Core 3.0 dotnet-script use extra nullable diagnostics

            var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty(TopLevelBinderFlagsProperty, BindingFlags.Instance | BindingFlags.NonPublic);
            var binderFlagsType = typeof(CSharpCompilationOptions).GetTypeInfo().Assembly.GetType(BinderFlagsType);

            var ignoreCorLibraryDuplicatedTypesMember = binderFlagsType?.GetField(IgnoreCorLibraryDuplicatedTypesField, BindingFlags.Static | BindingFlags.Public);
            var ignoreCorLibraryDuplicatedTypesValue = ignoreCorLibraryDuplicatedTypesMember?.GetValue(null);
            if (ignoreCorLibraryDuplicatedTypesValue != null)
            {
                topLevelBinderFlagsProperty?.SetValue(compilationOptions, ignoreCorLibraryDuplicatedTypesValue);
            }

            // in scripts, the option to supersede lower versions is ALWAYS enabled
            // see: https://github.com/dotnet/roslyn/blob/version-2.6.0-beta3/src/Compilers/CSharp/Portable/Compilation/CSharpCompilation.cs#L199
            var referencesSupersedeLowerVersionsProperty = typeof(CompilationOptions).GetProperty(ReferencesSupersedeLowerVersionsProperty, BindingFlags.Instance | BindingFlags.NonPublic);
            referencesSupersedeLowerVersionsProperty?.SetValue(compilationOptions, true);

            return compilationOptions;
        }

        private ScriptSourceResolver CreateScriptSourceResolver(IEnumerable<string> searchPaths)
        {
            var defaultResolver = ScriptSourceResolver.Default;

            if (searchPaths != null)
            {
                defaultResolver = defaultResolver.WithSearchPaths(searchPaths);

                foreach (string path in searchPaths)
                {
                    _logger.LogInformation($"CSX source path: {path}.");
                }
            }

            return defaultResolver;
        }

        private CachingScriptMetadataResolver CreateMetadataReferenceResolver(IEnumerable<string> searchPaths)
        {
            var defaultResolver = ScriptMetadataResolver.Default.WithBaseDirectory(_env.TargetDirectory);

            if (searchPaths != null)
            {
                defaultResolver = defaultResolver.WithSearchPaths(searchPaths);

                foreach (string path in searchPaths)
                {
                    _logger.LogInformation($"CSX reference path: {path}.");
                }
            }

            InjectXMLDocumentationProviderIntoRuntimeMetadataReferenceResolver(defaultResolver);

            var decoratedResolver = _scriptOptions.EnableScriptNuGetReferences
                ? new CachingScriptMetadataResolver(new NuGetMetadataReferenceResolver(defaultResolver))
                : new CachingScriptMetadataResolver(defaultResolver);

            return decoratedResolver;
        }

        public ProjectInfo CreateProject(string csxFileName, IEnumerable<MetadataReference> references, string csxFilePath, Type globalsType, IEnumerable<string> namespaces = null)
        {
            var csharpCommandLineArguments = _commandLineArgs.Value;

            // if RSP file was used, include the metadata references from RSP merged with the provided set
            // otherwise just use the provided metadata references
            if (csharpCommandLineArguments != null && csharpCommandLineArguments.MetadataReferences.Any())
            {
                var resolvedRspReferences = csharpCommandLineArguments.ResolveMetadataReferences(_compilationOptions.Value.MetadataReferenceResolver);
                foreach (var resolvedRspReference in resolvedRspReferences)
                {
                    if (resolvedRspReference is UnresolvedMetadataReference)
                    {
                        _logger.LogWarning($"{csxFileName} project. Skipping RSP reference to: {resolvedRspReference.Display} as it can't be resolved.");
                    }
                    else
                    {
                        _logger.LogDebug($"{csxFileName} project. Adding RSP reference to: {resolvedRspReference.Display}");
                    }
                }

                references = resolvedRspReferences.
                    Where(reference => !(reference is UnresolvedMetadataReference)).
                    Union(references, MetadataReferenceEqualityComparer.Instance);
            }

            var projectId = ProjectId.CreateNewId();
            var analyzerConfigDocuments = _editorConfigEnabled
                ? EditorConfigFinder
                    .GetEditorConfigPaths(csxFilePath)
                    .Select(path =>
                        DocumentInfo.Create(
                            DocumentId.CreateNewId(projectId),
                            name: ".editorconfig",
                            loader: new FileTextLoader(path, Encoding.UTF8),
                            filePath: path))
                    .ToImmutableArray()
                : ImmutableArray<DocumentInfo>.Empty;

            var project = ProjectInfo.Create(
                filePath: csxFilePath,
                id: projectId,
                version: VersionStamp.Create(),
                name: csxFileName,
                assemblyName: $"{csxFileName}.dll",
                language: LanguageNames.CSharp,
                compilationOptions: namespaces == null
                    ? _compilationOptions.Value
                    : _compilationOptions.Value.WithUsings(namespaces),
                metadataReferences: references,
                parseOptions: ParseOptions,
                isSubmission: true,
                hostObjectType: globalsType)
                .WithAnalyzerConfigDocuments(analyzerConfigDocuments);

            return project;
        }

        private void InjectXMLDocumentationProviderIntoRuntimeMetadataReferenceResolver(ScriptMetadataResolver resolver)
        {
            var runtimeMetadataReferenceResolverField = typeof(ScriptMetadataResolver).GetField(ResolverField, BindingFlags.Instance | BindingFlags.NonPublic);
            var runtimeMetadataReferenceResolverValue = runtimeMetadataReferenceResolverField?.GetValue(resolver);

            if (runtimeMetadataReferenceResolverValue != null)
            {
                var runtimeMetadataReferenceResolverType = typeof(CommandLineScriptGlobals).GetTypeInfo().Assembly.GetType(RuntimeMetadataReferenceResolverType);
                var fileReferenceProviderField = runtimeMetadataReferenceResolverType?.GetField(FileReferenceProviderField, BindingFlags.Instance | BindingFlags.NonPublic);
                fileReferenceProviderField.SetValue(runtimeMetadataReferenceResolverValue, new Func<string, MetadataReferenceProperties, PortableExecutableReference>((path, properties) =>
                {
                    var documentationFile = Path.ChangeExtension(path, ".xml");
                    var documentationProvider = File.Exists(documentationFile)
                        ? XmlDocumentationProvider.CreateFromFile(documentationFile)
                        : null;

                    return MetadataReference.CreateFromFile(path, properties, documentationProvider);
                }));
            }
        }
    }
}
