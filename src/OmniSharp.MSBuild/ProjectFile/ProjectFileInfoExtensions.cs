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
                analyzerReferences: new AnalyzerReference[] { new AnalyzerFileReference(@"C:\RoslynAnalyzers\Roslynator.CSharp.Analyzers.dll", new AnalyzerAssemblyLoader()) });
        }
    }

    public class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
        }

        public Assembly LoadFromPath(string fullPath)
        {
            return Assembly.LoadFrom(@"C:\RoslynAnalyzers\Roslynator.CSharp.Analyzers.dll");
        }
    }
}
