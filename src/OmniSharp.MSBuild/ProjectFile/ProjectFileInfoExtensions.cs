using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Newtonsoft.Json;
using OmniSharp.Helpers;

namespace OmniSharp.MSBuild.ProjectFile
{
    internal static class ProjectFileInfoExtensions
    {
        public static CSharpCompilationOptions CreateCompilationOptions(this ProjectFileInfo projectFileInfo)
        {
            var result = new CSharpCompilationOptions(projectFileInfo.OutputKind);

            result = result.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default);

            if (projectFileInfo.AllowUnsafeCode)
            {
                result = result.WithAllowUnsafe(true);
            }

            result = result.WithSpecificDiagnosticOptions(CompilationOptionsHelper.GetDefaultSuppressedDiagnosticOptions(projectFileInfo.SuppressedDiagnosticIds));

            if (projectFileInfo.SignAssembly && !string.IsNullOrEmpty(projectFileInfo.AssemblyOriginatorKeyFile))
            {
                var keyFile = Path.Combine(projectFileInfo.Directory, projectFileInfo.AssemblyOriginatorKeyFile);
                result = result.WithStrongNameProvider(new DesktopStrongNameProvider())
                               .WithCryptoKeyFile(keyFile);
            }

            if (!string.IsNullOrWhiteSpace(projectFileInfo.DocumentationFile))
            {
                result = result.WithXmlReferenceResolver(XmlFileResolver.Default);
            }

            return result;
        }

        public static ProjectInfo CreateProjectInfo(this ProjectFileInfo projectFileInfo)
        {
            var tempHardCodedPath = @"C:\RoslynAnalyzers\Roslynator.CSharp.Analyzers.dll";

            // SAVPEK TODO: Add analyzer references here!
            return ProjectInfo.Create(
                id: projectFileInfo.Id,
                version: VersionStamp.Create(),
                name: projectFileInfo.Name,
                assemblyName: projectFileInfo.AssemblyName,
                language: LanguageNames.CSharp,
                filePath: projectFileInfo.FilePath,
                outputFilePath: projectFileInfo.TargetPath,
                compilationOptions: projectFileInfo.CreateCompilationOptions(),
                analyzerReferences: new[] { new OmnisharpAnalyzerReference(tempHardCodedPath)});
        }
    }

    public class OmnisharpAnalyzerReference : AnalyzerReference
    {
        private readonly string assemblyPath;
        private readonly string id;

        [JsonConstructor]
        public OmnisharpAnalyzerReference() :base() {}

        public OmnisharpAnalyzerReference(string assemblyPath)
        {
            this.assemblyPath = assemblyPath;
            this.id = Guid.NewGuid().ToString();
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

        public override string FullPath => this.assemblyPath;

        public override object Id => this.id;

        public override string Display => this.assemblyPath;

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language)
        {
            var assembly = Assembly.LoadFrom(assemblyPath);

            var types = assembly.GetTypes()
                .Where(type => !type.GetTypeInfo().IsInterface &&
                               !type.GetTypeInfo().IsAbstract &&
                               !type.GetTypeInfo().ContainsGenericParameters)
                               .ToList();

            return types
                .Where(t => typeof(DiagnosticAnalyzer).IsAssignableFrom(t))
                .Select(type => CreateInstance<DiagnosticAnalyzer>(type))
                .Where(instance => instance != null)
                .ToList()
                .ToImmutableArray();
        }

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages()
        {
            var assembly = Assembly.LoadFrom(assemblyPath);

            var types = assembly.GetTypes()
                .Where(type => !type.GetTypeInfo().IsInterface &&
                               !type.GetTypeInfo().IsAbstract &&
                               !type.GetTypeInfo().ContainsGenericParameters)
                               .ToList();

            return types
                .Where(t => typeof(DiagnosticAnalyzer).IsAssignableFrom(t))
                .Select(type => CreateInstance<DiagnosticAnalyzer>(type))
                .Where(instance => instance != null)
                .ToList()
                .ToImmutableArray();
        }
    }
}
