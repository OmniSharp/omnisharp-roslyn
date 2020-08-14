using System.Collections.Generic;
using System.Collections.Immutable;
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

            compilationOptions = compilationOptions.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default)
                                    .WithSpecificDiagnosticOptions(projectFileInfo.GetDiagnosticOptions())
                                    .WithOverflowChecks(projectFileInfo.CheckForOverflowUnderflow);

            if (projectFileInfo.AllowUnsafeCode)
            {
                compilationOptions = compilationOptions.WithAllowUnsafe(true);
            }

            if (projectFileInfo.TreatWarningsAsErrors)
            {
                compilationOptions = compilationOptions.WithGeneralDiagnosticOption(ReportDiagnostic.Error);
            }

            if (projectFileInfo.NullableContextOptions != compilationOptions.NullableContextOptions)
            {
                compilationOptions = compilationOptions.WithNullableContextOptions(projectFileInfo.NullableContextOptions);
            }

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

        public static ImmutableDictionary<string, ReportDiagnostic> GetDiagnosticOptions(this ProjectFileInfo projectFileInfo)
        {
            var suppressions = CompilationOptionsHelper.GetDefaultSuppressedDiagnosticOptions(projectFileInfo.SuppressedDiagnosticIds);
            var specificRules = projectFileInfo.RuleSet?.SpecificDiagnosticOptions ?? ImmutableDictionary<string, ReportDiagnostic>.Empty;

            // suppressions capture NoWarn and they have the highest priority
            var combinedRules = specificRules.Concat(suppressions.Where(x => !specificRules.Keys.Contains(x.Key))).ToDictionary(x => x.Key, x => x.Value);

            // then handle WarningsAsErrors
            foreach (var warningAsError in projectFileInfo.WarningsAsErrors)
            {
                if (!suppressions.ContainsKey(warningAsError))
                {
                    combinedRules[warningAsError] = ReportDiagnostic.Error;
                }
            }

            // WarningsNotAsErrors can overwrite WarningsAsErrors
            foreach (var warningNotAsError in projectFileInfo.WarningsNotAsErrors)
            {
                if (!suppressions.ContainsKey(warningNotAsError))
                {
                    combinedRules[warningNotAsError] = ReportDiagnostic.Warn;
                }
            }

            return combinedRules.ToImmutableDictionary();
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
                analyzerReferences: analyzerReferences).WithDefaultNamespace(projectFileInfo.DefaultNamespace);
        }

        private static IEnumerable<AnalyzerReference> ResolveAnalyzerReferencesForProject(ProjectFileInfo projectFileInfo, IAnalyzerAssemblyLoader analyzerAssemblyLoader)
        {
            if (!projectFileInfo.RunAnalyzers || !projectFileInfo.RunAnalyzersDuringLiveAnalysis)
            {
                return Enumerable.Empty<AnalyzerReference>();
            }

            foreach(var analyzerAssemblyPath in projectFileInfo.Analyzers.Distinct())
            {
                analyzerAssemblyLoader.AddDependencyLocation(analyzerAssemblyPath);
            }

            return projectFileInfo.Analyzers.Select(analyzerCandicatePath => new AnalyzerFileReference(analyzerCandicatePath, analyzerAssemblyLoader));
        }
    }
}
