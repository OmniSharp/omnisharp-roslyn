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

namespace OmniSharp.Script
{
    public class ScriptHelper
    {
        private const string BinderFlagsType = "Microsoft.CodeAnalysis.CSharp.BinderFlags";
        private const string TopLevelBinderFlagsProperty = "TopLevelBinderFlags";
        private const string IgnoreCorLibraryDuplicatedTypesField = "IgnoreCorLibraryDuplicatedTypes";
        private const string RuntimeMetadataReferenceResolverType = "Microsoft.CodeAnalysis.Scripting.Hosting.RuntimeMetadataReferenceResolver";
        private const string ResolverField = "_resolver";
        private const string FileReferenceProviderField = "_fileReferenceProvider";

        private readonly IConfiguration _configuration;

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

        public ScriptHelper(IConfiguration configuration = null)
        {
            _configuration = configuration;
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
                assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default).WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic>
                {
                    // ensure that specific warnings about assembly references are always suppressed
                    // https://github.com/dotnet/roslyn/issues/5501
                    {"CS1701", ReportDiagnostic.Suppress},
                    {"CS1702", ReportDiagnostic.Suppress},
                    {"CS1705", ReportDiagnostic.Suppress}
                });

            var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty(TopLevelBinderFlagsProperty, BindingFlags.Instance | BindingFlags.NonPublic);
            var binderFlagsType = typeof(CSharpCompilationOptions).GetTypeInfo().Assembly.GetType(BinderFlagsType);

            var ignoreCorLibraryDuplicatedTypesMember = binderFlagsType?.GetField(IgnoreCorLibraryDuplicatedTypesField, BindingFlags.Static | BindingFlags.Public);
            var ignoreCorLibraryDuplicatedTypesValue = ignoreCorLibraryDuplicatedTypesMember?.GetValue(null);
            if (ignoreCorLibraryDuplicatedTypesValue != null)
            {
                topLevelBinderFlagsProperty?.SetValue(compilationOptions, ignoreCorLibraryDuplicatedTypesValue);
            }

            return compilationOptions;
        }

        private CachingScriptMetadataResolver CreateMetadataReferenceResolver()
        {
            bool enableScriptNuGetReferences = false;

            if (_configuration != null)
            {
                if (!bool.TryParse(_configuration["enableScriptNuGetReferences"], out enableScriptNuGetReferences))
                {
                    enableScriptNuGetReferences = false;
                }
            }
            
            return enableScriptNuGetReferences
                ? new CachingScriptMetadataResolver(new NuGetMetadataReferenceResolver(_resolver))
                : new CachingScriptMetadataResolver(_resolver);
        }
 
        public ProjectInfo CreateProject(string csxFileName, IEnumerable<MetadataReference> references, IEnumerable<string> namespaces = null)
        {
            var project = ProjectInfo.Create(
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
