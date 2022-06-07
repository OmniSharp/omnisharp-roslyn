using System;
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
            return projectFileInfo.CreateCompilationOptions(compilationOptions);
        }

        public static CSharpCompilationOptions CreateCompilationOptions(this ProjectFileInfo projectFileInfo, CSharpCompilationOptions existingCompilationOptions)
        {
            var compilationOptions = existingCompilationOptions.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default)
                        .WithSpecificDiagnosticOptions(projectFileInfo.GetDiagnosticOptions())
                        .WithOverflowChecks(projectFileInfo.CheckForOverflowUnderflow);

            var platformTarget = ParsePlatform(projectFileInfo.PlatformTarget);
            if (platformTarget != compilationOptions.Platform)
            {
                compilationOptions = compilationOptions.WithPlatform(platformTarget);
            }

            if (projectFileInfo.AllowUnsafeCode != compilationOptions.AllowUnsafe)
            {
                compilationOptions = compilationOptions.WithAllowUnsafe(projectFileInfo.AllowUnsafeCode);
            }

            compilationOptions = projectFileInfo.TreatWarningsAsErrors ?
                        compilationOptions.WithGeneralDiagnosticOption(ReportDiagnostic.Error) : compilationOptions.WithGeneralDiagnosticOption(ReportDiagnostic.Default);

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
            var combinedRules = specificRules.Concat(suppressions.Where(x => !specificRules.ContainsKey(x.Key))).ToDictionary(x => x.Key, x => x.Value);

            // then handle WarningsAsErrors
            foreach (var warningAsError in projectFileInfo.WarningsAsErrors)
            {
                if (string.Equals(warningAsError, "nullable", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var id in Errors.NullableWarnings)
                    {
                        AddIfNotSuppressed(id, ReportDiagnostic.Error);
                    }
                }
                else 
                {
                    AddIfNotSuppressed(warningAsError, ReportDiagnostic.Error);
                }
            }

            // WarningsNotAsErrors can overwrite WarningsAsErrors
            foreach (var warningNotAsError in projectFileInfo.WarningsNotAsErrors)
            {
                AddIfNotSuppressed(warningNotAsError, ReportDiagnostic.Warn);
            }

            return combinedRules.ToImmutableDictionary();

            void AddIfNotSuppressed(string code, ReportDiagnostic diagnostic)
            {
                if (!suppressions.ContainsKey(code))
                {
                    combinedRules[code] = diagnostic;
                }
            }
        }

        public static ProjectInfo CreateProjectInfo(this ProjectFileInfo projectFileInfo, IAnalyzerAssemblyLoader analyzerAssemblyLoader)
        {
            var analyzerReferences = projectFileInfo.ResolveAnalyzerReferencesForProject(analyzerAssemblyLoader);

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

        public static ImmutableArray<AnalyzerFileReference> ResolveAnalyzerReferencesForProject(this ProjectFileInfo projectFileInfo, IAnalyzerAssemblyLoader analyzerAssemblyLoader)
        {
            if (!projectFileInfo.RunAnalyzers || !projectFileInfo.RunAnalyzersDuringLiveAnalysis)
            {
                return ImmutableArray<AnalyzerFileReference>.Empty;
            }

            foreach(var analyzerAssemblyPath in projectFileInfo.Analyzers.Distinct())
            {
                analyzerAssemblyLoader.AddDependencyLocation(analyzerAssemblyPath);
            }

            return projectFileInfo.Analyzers.Select(analyzerCandicatePath => new AnalyzerFileReference(analyzerCandicatePath, analyzerAssemblyLoader)).ToImmutableArray();
        }

        private static Platform ParsePlatform(string value) => (value?.ToLowerInvariant()) switch
        {
            "x86" => Platform.X86,
            "x64" => Platform.X64,
            "itanium" => Platform.Itanium,
            "anycpu" => Platform.AnyCpu,
            "anycpu32bitpreferred" => Platform.AnyCpu32BitPreferred,
            "arm" => Platform.Arm,
            "arm64" => Platform.Arm64,
            _ => Platform.AnyCpu,
        };
    }
}
