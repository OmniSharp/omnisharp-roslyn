using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Helpers;

namespace OmniSharp.MSBuild.ProjectFile
{
    internal static class ProjectFileInfoExtensions
    {
        public static CSharpCompilationOptions CreateCompilationOptions(this ProjectFileInfo projectFileInfo)
        {
            var compilationOptions = new CSharpCompilationOptions(projectFileInfo.OutputKind);

            compilationOptions = compilationOptions.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default);

            if (projectFileInfo.AllowUnsafeCode)
            {
                compilationOptions = compilationOptions.WithAllowUnsafe(true).WithGeneralDiagnosticOption(ReportDiagnostic.Error);
            }

            if (projectFileInfo.TreatWarningsAsErrors)
            {
                compilationOptions = compilationOptions.WithGeneralDiagnosticOption(ReportDiagnostic.Error);
            }

            if (projectFileInfo.NullableContextOptions != compilationOptions.NullableContextOptions)
            {
                compilationOptions = compilationOptions.WithNullableContextOptions(projectFileInfo.NullableContextOptions);
            }

            compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(CompilationOptionsHelper.GetDefaultSuppressedDiagnosticOptions(projectFileInfo.SuppressedDiagnosticIds));

            if (projectFileInfo.SignAssembly && !string.IsNullOrEmpty(projectFileInfo.AssemblyOriginatorKeyFile))
            {
                var keyFile = Path.Combine(projectFileInfo.Directory, projectFileInfo.AssemblyOriginatorKeyFile);
                compilationOptions = compilationOptions.WithStrongNameProvider(new DesktopStrongNameProvider())
                               .WithCryptoKeyFile(keyFile);
            }

            if (!string.IsNullOrWhiteSpace(projectFileInfo.DocumentationFile))
            {
                compilationOptions = compilationOptions.WithXmlReferenceResolver(XmlFileResolver.Default);
            }

            return compilationOptions;
        }

        public static ProjectInfo CreateProjectInfo(this ProjectFileInfo projectFileInfo, IAnalyzerAssemblyLoader analyzerAssemblyLoader)
        {
            var analyzerReferences = ResolveAnalyzerReferencesForProject(projectFileInfo, analyzerAssemblyLoader);

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

        private static IEnumerable<AnalyzerReference> ResolveAnalyzerReferencesForProject(ProjectFileInfo projectFileInfo, IAnalyzerAssemblyLoader analyzerAssemblyLoader)
        {
            foreach(var analyzerAssemblyPath in projectFileInfo.Analyzers.Distinct())
            {
                analyzerAssemblyLoader.AddDependencyLocation(analyzerAssemblyPath);
            }

            return projectFileInfo.Analyzers.Select(analyzerCandicatePath => new AnalyzerFileReference(analyzerCandicatePath, analyzerAssemblyLoader));
        }
    }
}
