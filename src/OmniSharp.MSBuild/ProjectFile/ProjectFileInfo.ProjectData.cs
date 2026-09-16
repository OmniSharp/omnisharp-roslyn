using System;
using System.Collections.Immutable;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild.ProjectFile
{
    internal partial class ProjectFileInfo
    {
        private partial class ProjectData
        {
            public Guid Guid { get; }
            public string Name { get; }

            public string AssemblyName { get; }
            public string TargetPath { get; }
            public string OutputPath { get; }
            public string IntermediateOutputPath { get; }
            public string ProjectAssetsFile { get; }

            public string Configuration { get; }
            public string Platform { get; }
            public string PlatformTarget { get; }
            public FrameworkName TargetFramework { get; }
            public ImmutableArray<string> TargetFrameworks { get; }

            public OutputKind OutputKind { get; }
            public LanguageVersion LanguageVersion { get; }
            public NullableContextOptions NullableContextOptions { get; }
            public bool AllowUnsafeCode { get; }
            public bool CheckForOverflowUnderflow { get; }
            public string DocumentationFile { get; }
            public ImmutableArray<string> PreprocessorSymbolNames { get; }
            public ImmutableArray<string> SuppressedDiagnosticIds { get; }

            public bool SignAssembly { get; }
            public string AssemblyOriginatorKeyFile { get; }

            public ImmutableArray<ProjectFileGlob> FileInclusionGlobs { get; }
            public ImmutableArray<string> ProjectCapabilities { get; private set; }
            public ImmutableArray<string> ContentFilePaths { get; private set; }
            public CSharpCompilationOptions BuildHostCompilationOptions { get; private set; }
            public CSharpParseOptions BuildHostParseOptions { get; private set; }
            public ImmutableArray<string> SourceFiles { get; }
            public ImmutableArray<string> ProjectReferences { get; }
            public ImmutableArray<string> References { get; }
            public ImmutableArray<PackageReference> PackageReferences { get; }
            public ImmutableArray<string> Analyzers { get; }
            public ImmutableArray<string> AdditionalFiles { get; }
            public ImmutableArray<string> AnalyzerConfigFiles { get; }
            public ImmutableArray<string> WarningsAsErrors { get; }
            public ImmutableArray<string> WarningsNotAsErrors { get; }
            public RuleSet RuleSet { get; }
            public ImmutableDictionary<string, string> ReferenceAliases { get; }
            public ImmutableDictionary<string, string> ProjectReferenceAliases { get; }
            public bool TreatWarningsAsErrors { get; }
            public bool RunAnalyzers { get; }
            public bool RunAnalyzersDuringLiveAnalysis { get; }
            public string DefaultNamespace { get; }

            private ProjectData()
            {
                // Be sure to initialize all collection properties with ImmutableArray<T>.Empty.
                // Otherwise, Json.net won't be able to serialize the values.
                TargetFrameworks = ImmutableArray<string>.Empty;
                PreprocessorSymbolNames = ImmutableArray<string>.Empty;
                SuppressedDiagnosticIds = ImmutableArray<string>.Empty;
                SourceFiles = ImmutableArray<string>.Empty;
                ProjectReferences = ImmutableArray<string>.Empty;
                References = ImmutableArray<string>.Empty;
                PackageReferences = ImmutableArray<PackageReference>.Empty;
                Analyzers = ImmutableArray<string>.Empty;
                AdditionalFiles = ImmutableArray<string>.Empty;
                AnalyzerConfigFiles = ImmutableArray<string>.Empty;
                ReferenceAliases = ImmutableDictionary<string, string>.Empty;
                ProjectReferenceAliases = ImmutableDictionary<string, string>.Empty;
                WarningsAsErrors = ImmutableArray<string>.Empty;
                FileInclusionGlobs = ImmutableArray<ProjectFileGlob>.Empty;
                WarningsNotAsErrors = ImmutableArray<string>.Empty;
                ProjectCapabilities = ImmutableArray<string>.Empty;
                ContentFilePaths = ImmutableArray<string>.Empty;
            }

            private ProjectData(
                Guid guid, string name,
                string assemblyName, string targetPath, string outputPath, string intermediateOutputPath,
                string projectAssetsFile,
                string configuration, string platform, string platformTarget,
                FrameworkName targetFramework,
                ImmutableArray<string> targetFrameworks,
                OutputKind outputKind,
                LanguageVersion languageVersion,
                NullableContextOptions nullableContextOptions,
                bool allowUnsafeCode,
                bool checkForOverflowUnderflow,
                string documentationFile,
                ImmutableArray<string> preprocessorSymbolNames,
                ImmutableArray<string> suppressedDiagnosticIds,
                ImmutableArray<string> warningsAsErrors,
                ImmutableArray<string> warningsNotAsErrors,
                bool signAssembly,
                string assemblyOriginatorKeyFile,
                bool treatWarningsAsErrors,
                string defaultNamespace,
                bool runAnalyzers,
                bool runAnalyzersDuringLiveAnalysis,
                RuleSet ruleset)
                : this()
            {
                Guid = guid;
                Name = name;

                AssemblyName = assemblyName;
                TargetPath = targetPath;
                OutputPath = outputPath;
                IntermediateOutputPath = intermediateOutputPath;
                ProjectAssetsFile = projectAssetsFile;

                Configuration = configuration;
                Platform = platform;
                PlatformTarget = platformTarget;
                TargetFramework = targetFramework;
                TargetFrameworks = targetFrameworks.EmptyIfDefault();

                OutputKind = outputKind;
                LanguageVersion = languageVersion;
                NullableContextOptions = nullableContextOptions;
                AllowUnsafeCode = allowUnsafeCode;
                CheckForOverflowUnderflow = checkForOverflowUnderflow;
                DocumentationFile = documentationFile;
                PreprocessorSymbolNames = preprocessorSymbolNames.EmptyIfDefault();
                SuppressedDiagnosticIds = suppressedDiagnosticIds.EmptyIfDefault();
                WarningsAsErrors = warningsAsErrors.EmptyIfDefault();
                WarningsNotAsErrors = warningsNotAsErrors.EmptyIfDefault();

                SignAssembly = signAssembly;
                AssemblyOriginatorKeyFile = assemblyOriginatorKeyFile;
                TreatWarningsAsErrors = treatWarningsAsErrors;
                RuleSet = ruleset;
                DefaultNamespace = defaultNamespace;

                RunAnalyzers = runAnalyzers;
                RunAnalyzersDuringLiveAnalysis = runAnalyzersDuringLiveAnalysis;
            }

            private ProjectData(
                Guid guid, string name,
                string assemblyName, string targetPath, string outputPath, string intermediateOutputPath,
                string projectAssetsFile,
                string configuration, string platform, string platformTarget,
                FrameworkName targetFramework,
                ImmutableArray<string> targetFrameworks,
                OutputKind outputKind,
                LanguageVersion languageVersion,
                NullableContextOptions nullableContextOptions,
                bool allowUnsafeCode,
                bool checkForOverflowUnderflow,
                string documentationFile,
                ImmutableArray<string> preprocessorSymbolNames,
                ImmutableArray<string> suppressedDiagnosticIds,
                ImmutableArray<string> warningsAsErrors,
                ImmutableArray<string> warningsNotAsErrors,
                bool signAssembly,
                string assemblyOriginatorKeyFile,
                ImmutableArray<string> sourceFiles,
                ImmutableArray<string> projectReferences,
                ImmutableArray<string> references,
                ImmutableArray<PackageReference> packageReferences,
                ImmutableArray<string> analyzers,
                ImmutableArray<string> additionalFiles,
                ImmutableArray<string> analyzerConfigFiles,
                bool treatWarningsAsErrors,
                string defaultNamespace,
                bool runAnalyzers,
                bool runAnalyzersDuringLiveAnalysis,
                RuleSet ruleset,
                ImmutableDictionary<string, string> referenceAliases,
                ImmutableDictionary<string, string> projectReferenceAliases,
                ImmutableArray<ProjectFileGlob> fileInclusionGlobs)
                : this(guid, name, assemblyName, targetPath, outputPath, intermediateOutputPath, projectAssetsFile,
                      configuration, platform, platformTarget, targetFramework, targetFrameworks, outputKind, languageVersion, nullableContextOptions, allowUnsafeCode, checkForOverflowUnderflow,
                      documentationFile, preprocessorSymbolNames, suppressedDiagnosticIds, warningsAsErrors, warningsNotAsErrors, signAssembly, assemblyOriginatorKeyFile, treatWarningsAsErrors, defaultNamespace, runAnalyzers, runAnalyzersDuringLiveAnalysis, ruleset)
            {
                SourceFiles = sourceFiles.EmptyIfDefault();
                ProjectReferences = projectReferences.EmptyIfDefault();
                References = references.EmptyIfDefault();
                PackageReferences = packageReferences.EmptyIfDefault();
                Analyzers = analyzers.EmptyIfDefault();
                AdditionalFiles = additionalFiles.EmptyIfDefault();
                AnalyzerConfigFiles = analyzerConfigFiles.EmptyIfDefault();
                ReferenceAliases = referenceAliases;
                ProjectReferenceAliases = projectReferenceAliases;
                FileInclusionGlobs = fileInclusionGlobs;
            }

        }
    }
}
