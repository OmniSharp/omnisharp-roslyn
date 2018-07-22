using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var analyzerReferences = ResolveAnalyzerReferencesForProject(projectFileInfo);

            return ProjectInfo.Create(
                id: projectFileInfo.Id,
                version: VersionStamp.Create(),
                name: projectFileInfo.Name,
                assemblyName: projectFileInfo.AssemblyName,
                language: LanguageNames.CSharp,
                filePath: projectFileInfo.FilePath,
                outputFilePath: projectFileInfo.TargetPath,
                compilationOptions: projectFileInfo.CreateCompilationOptions(),
                analyzerReferences: analyzerReferences);
        }

        private static IEnumerable<AnalyzerFileReference> ResolveAnalyzerReferencesForProject(ProjectFileInfo projectFileInfo)
        {
            return projectFileInfo.Analyzers
                .GroupBy(x => Path.GetDirectoryName(x))
                .Select(singleAnalyzerPackageGroup =>
                {
                    // Is there better way to figure out entry assembly for specific nuget analyzer package?
                    var analyzerMainAssembly = singleAnalyzerPackageGroup.Single(x => x.EndsWith("Analyzers.dll") || x.EndsWith("Analyzer.dll"));

                    var assemblyLoader = new AnalyzerAssemblyLoader();
                    singleAnalyzerPackageGroup.ToList().ForEach(x => assemblyLoader.AddDependencyLocation(x));

                    return new AnalyzerFileReference(analyzerMainAssembly, assemblyLoader);
                });
        }
    }
}
