using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Dotnet.Script.DependencyModel.NuGet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.Extensions.Configuration;
using OmniSharp.Helpers;

namespace OmniSharp.Script
{
    public class ScriptHelper
    {
        private const string BinderFlagsType = "Microsoft.CodeAnalysis.CSharp.BinderFlags";
        private const string TopLevelBinderFlagsProperty = "TopLevelBinderFlags";
        private const string ReferencesSupersedeLowerVersionsProperty = "ReferencesSupersedeLowerVersions_internal_protected_set";
        private const string IgnoreCorLibraryDuplicatedTypesField = "IgnoreCorLibraryDuplicatedTypes";
        private const string RuntimeMetadataReferenceResolverType = "Microsoft.CodeAnalysis.Scripting.Hosting.RuntimeMetadataReferenceResolver";
        private const string ResolverField = "_resolver";
        private const string FileReferenceProviderField = "_fileReferenceProvider";

        private readonly ScriptOptions _scriptOptions;

        // aligned with CSI.exe
        // https://github.com/dotnet/roslyn/blob/version-2.0.0-rc3/src/Interactive/csi/csi.rsp
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
        private readonly MetadataReferenceResolver _resolver = ScriptMetadataResolver.Default;

        public ScriptHelper(ScriptOptions scriptOptions)
        {
            _scriptOptions = scriptOptions;
            _compilationOptions = new Lazy<CSharpCompilationOptions>(CreateCompilationOptions);
            InjectXMLDocumentationProviderIntoRuntimeMetadataReferenceResolver();
        }

        private CSharpCompilationOptions CreateCompilationOptions()
        {
            var compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                usings: DefaultNamespaces,
                allowUnsafe: true,
                metadataReferenceResolver:
                CreateMetadataReferenceResolver(),
                sourceReferenceResolver: ScriptSourceResolver.Default,
                assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default).
                    WithSpecificDiagnosticOptions(CompilationOptionsHelper.GetDefaultSuppressedDiagnosticOptions());

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

        private CachingScriptMetadataResolver CreateMetadataReferenceResolver()
        {
            return _scriptOptions.IsNugetEnabled()
                ? new CachingScriptMetadataResolver(new NuGetMetadataReferenceResolver(_resolver))
                : new CachingScriptMetadataResolver(_resolver);
        }
 
        public ProjectInfo CreateProject(string csxFileName, IEnumerable<MetadataReference> references, string csxFilePath, IEnumerable<string> namespaces = null)
        {
            var project = ProjectInfo.Create(
                filePath: csxFilePath,
                id: ProjectId.CreateNewId(),
                version: VersionStamp.Create(),
                name: csxFileName,
                assemblyName: $"{csxFileName}.dll",
                language: LanguageNames.CSharp,
                compilationOptions: namespaces == null ? _compilationOptions.Value : _compilationOptions.Value.WithUsings(namespaces),
                metadataReferences: references,
                parseOptions: ParseOptions,
                isSubmission: true,
                hostObjectType: typeof(CommandLineScriptGlobals));

            return project;
        }

        private void InjectXMLDocumentationProviderIntoRuntimeMetadataReferenceResolver()
        {
            var runtimeMetadataReferenceResolverField = typeof(ScriptMetadataResolver).GetField(ResolverField, BindingFlags.Instance | BindingFlags.NonPublic);
            var runtimeMetadataReferenceResolverValue = runtimeMetadataReferenceResolverField?.GetValue(_resolver);

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
