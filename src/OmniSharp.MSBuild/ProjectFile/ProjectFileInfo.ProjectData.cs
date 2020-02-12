using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuGet.Packaging.Core;
using OmniSharp.Utilities;

using MSB = Microsoft.Build;

namespace OmniSharp.MSBuild.ProjectFile
{
    internal partial class ProjectFileInfo
    {
        private class ProjectData
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

            public ImmutableArray<string> SourceFiles { get; }
            public ImmutableArray<string> ProjectReferences { get; }
            public ImmutableArray<string> References { get; }
            public ImmutableArray<PackageReference> PackageReferences { get; }
            public ImmutableArray<string> Analyzers { get; }
            public ImmutableArray<string> AdditionalFiles { get; }
            public RuleSet RuleSet { get; }
            public ImmutableDictionary<string, string> ReferenceAliases { get; }
            public ImmutableDictionary<string, string> ProjectReferenceAliases { get; }
            public bool TreatWarningsAsErrors { get; }
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
                ReferenceAliases = ImmutableDictionary<string, string>.Empty;
                ProjectReferenceAliases = ImmutableDictionary<string, string>.Empty;
            }

            private ProjectData(
                Guid guid, string name,
                string assemblyName, string targetPath, string outputPath, string intermediateOutputPath,
                string projectAssetsFile,
                string configuration, string platform,
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
                bool signAssembly,
                string assemblyOriginatorKeyFile,
                bool treatWarningsAsErrors,
                string defaultNamespace,
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

                SignAssembly = signAssembly;
                AssemblyOriginatorKeyFile = assemblyOriginatorKeyFile;
                TreatWarningsAsErrors = treatWarningsAsErrors;
                RuleSet = ruleset;
                DefaultNamespace = defaultNamespace;
            }

            private ProjectData(
                Guid guid, string name,
                string assemblyName, string targetPath, string outputPath, string intermediateOutputPath,
                string projectAssetsFile,
                string configuration, string platform,
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
                bool signAssembly,
                string assemblyOriginatorKeyFile,
                ImmutableArray<string> sourceFiles,
                ImmutableArray<string> projectReferences,
                ImmutableArray<string> references,
                ImmutableArray<PackageReference> packageReferences,
                ImmutableArray<string> analyzers,
                ImmutableArray<string> additionalFiles,
                bool treatWarningsAsErrors,
                string defaultNamespace,
                RuleSet ruleset,
                ImmutableDictionary<string, string> referenceAliases,
                ImmutableDictionary<string, string> projectReferenceAliases)
                : this(guid, name, assemblyName, targetPath, outputPath, intermediateOutputPath, projectAssetsFile,
                      configuration, platform, targetFramework, targetFrameworks, outputKind, languageVersion, nullableContextOptions, allowUnsafeCode, checkForOverflowUnderflow,
                      documentationFile, preprocessorSymbolNames, suppressedDiagnosticIds, signAssembly, assemblyOriginatorKeyFile, treatWarningsAsErrors, defaultNamespace, ruleset)
            {
                SourceFiles = sourceFiles.EmptyIfDefault();
                ProjectReferences = projectReferences.EmptyIfDefault();
                References = references.EmptyIfDefault();
                PackageReferences = packageReferences.EmptyIfDefault();
                Analyzers = analyzers.EmptyIfDefault();
                AdditionalFiles = additionalFiles.EmptyIfDefault();
                ReferenceAliases = referenceAliases;
                ProjectReferenceAliases = projectReferenceAliases;
            }

            public static ProjectData Create(MSB.Evaluation.Project project)
            {
                var guid = PropertyConverter.ToGuid(project.GetPropertyValue(PropertyNames.ProjectGuid));
                var name = project.GetPropertyValue(PropertyNames.ProjectName);
                var assemblyName = project.GetPropertyValue(PropertyNames.AssemblyName);
                var targetPath = project.GetPropertyValue(PropertyNames.TargetPath);
                var outputPath = project.GetPropertyValue(PropertyNames.OutputPath);
                var intermediateOutputPath = project.GetPropertyValue(PropertyNames.IntermediateOutputPath);
                var projectAssetsFile = project.GetPropertyValue(PropertyNames.ProjectAssetsFile);
                var configuration = project.GetPropertyValue(PropertyNames.Configuration);
                var platform = project.GetPropertyValue(PropertyNames.Platform);
                var defaultNamespace = project.GetPropertyValue(PropertyNames.RootNamespace);

                var targetFramework = new FrameworkName(project.GetPropertyValue(PropertyNames.TargetFrameworkMoniker));

                var targetFrameworkValue = project.GetPropertyValue(PropertyNames.TargetFramework);
                var targetFrameworks = PropertyConverter.SplitList(project.GetPropertyValue(PropertyNames.TargetFrameworks), ';');

                if (!string.IsNullOrWhiteSpace(targetFrameworkValue) && targetFrameworks.Length == 0)
                {
                    targetFrameworks = ImmutableArray.Create(targetFrameworkValue);
                }

                var languageVersion = PropertyConverter.ToLanguageVersion(project.GetPropertyValue(PropertyNames.LangVersion));
                var allowUnsafeCode = PropertyConverter.ToBoolean(project.GetPropertyValue(PropertyNames.AllowUnsafeBlocks), defaultValue: false);
                var checkForOverflowUnderflow = PropertyConverter.ToBoolean(project.GetPropertyValue(PropertyNames.CheckForOverflowUnderflow), defaultValue: false);
                var outputKind = PropertyConverter.ToOutputKind(project.GetPropertyValue(PropertyNames.OutputType));
                var nullableContextOptions = PropertyConverter.ToNullableContextOptions(project.GetPropertyValue(PropertyNames.Nullable));
                var documentationFile = project.GetPropertyValue(PropertyNames.DocumentationFile);
                var preprocessorSymbolNames = PropertyConverter.ToPreprocessorSymbolNames(project.GetPropertyValue(PropertyNames.DefineConstants));
                var suppressedDiagnosticIds = PropertyConverter.ToSuppressedDiagnosticIds(project.GetPropertyValue(PropertyNames.NoWarn));
                var signAssembly = PropertyConverter.ToBoolean(project.GetPropertyValue(PropertyNames.SignAssembly), defaultValue: false);
                var assemblyOriginatorKeyFile = project.GetPropertyValue(PropertyNames.AssemblyOriginatorKeyFile);
                var treatWarningsAsErrors = PropertyConverter.ToBoolean(project.GetPropertyValue(PropertyNames.TreatWarningsAsErrors), defaultValue: false);

                return new ProjectData(
                    guid, name, assemblyName, targetPath, outputPath, intermediateOutputPath, projectAssetsFile,
                    configuration, platform, targetFramework, targetFrameworks, outputKind, languageVersion, nullableContextOptions, allowUnsafeCode, checkForOverflowUnderflow,
                    documentationFile, preprocessorSymbolNames, suppressedDiagnosticIds, signAssembly, assemblyOriginatorKeyFile, treatWarningsAsErrors, defaultNamespace, ruleset: null);
            }

            public static ProjectData Create(MSB.Execution.ProjectInstance projectInstance)
            {
                var guid = PropertyConverter.ToGuid(projectInstance.GetPropertyValue(PropertyNames.ProjectGuid));
                var name = projectInstance.GetPropertyValue(PropertyNames.ProjectName);
                var assemblyName = projectInstance.GetPropertyValue(PropertyNames.AssemblyName);
                var targetPath = projectInstance.GetPropertyValue(PropertyNames.TargetPath);
                var outputPath = projectInstance.GetPropertyValue(PropertyNames.OutputPath);
                var intermediateOutputPath = projectInstance.GetPropertyValue(PropertyNames.IntermediateOutputPath);
                var projectAssetsFile = projectInstance.GetPropertyValue(PropertyNames.ProjectAssetsFile);
                var configuration = projectInstance.GetPropertyValue(PropertyNames.Configuration);
                var platform = projectInstance.GetPropertyValue(PropertyNames.Platform);
                var defaultNamespace = projectInstance.GetPropertyValue(PropertyNames.RootNamespace);

                var targetFramework = new FrameworkName(projectInstance.GetPropertyValue(PropertyNames.TargetFrameworkMoniker));

                var targetFrameworkValue = projectInstance.GetPropertyValue(PropertyNames.TargetFramework);
                var targetFrameworks = PropertyConverter.SplitList(projectInstance.GetPropertyValue(PropertyNames.TargetFrameworks), ';');

                if (!string.IsNullOrWhiteSpace(targetFrameworkValue) && targetFrameworks.Length == 0)
                {
                    targetFrameworks = ImmutableArray.Create(targetFrameworkValue);
                }

                var languageVersion = PropertyConverter.ToLanguageVersion(projectInstance.GetPropertyValue(PropertyNames.LangVersion));
                var allowUnsafeCode = PropertyConverter.ToBoolean(projectInstance.GetPropertyValue(PropertyNames.AllowUnsafeBlocks), defaultValue: false);
                var checkForOverflowUnderflow = PropertyConverter.ToBoolean(projectInstance.GetPropertyValue(PropertyNames.CheckForOverflowUnderflow), defaultValue: false);
                var outputKind = PropertyConverter.ToOutputKind(projectInstance.GetPropertyValue(PropertyNames.OutputType));
                var nullableContextOptions = PropertyConverter.ToNullableContextOptions(projectInstance.GetPropertyValue(PropertyNames.Nullable));
                var documentationFile = projectInstance.GetPropertyValue(PropertyNames.DocumentationFile);
                var preprocessorSymbolNames = PropertyConverter.ToPreprocessorSymbolNames(projectInstance.GetPropertyValue(PropertyNames.DefineConstants));
                var suppressedDiagnosticIds = PropertyConverter.ToSuppressedDiagnosticIds(projectInstance.GetPropertyValue(PropertyNames.NoWarn));
                var signAssembly = PropertyConverter.ToBoolean(projectInstance.GetPropertyValue(PropertyNames.SignAssembly), defaultValue: false);
                var treatWarningsAsErrors = PropertyConverter.ToBoolean(projectInstance.GetPropertyValue(PropertyNames.TreatWarningsAsErrors), defaultValue: false);
                var assemblyOriginatorKeyFile = projectInstance.GetPropertyValue(PropertyNames.AssemblyOriginatorKeyFile);

                var ruleset = ResolveRulesetIfAny(projectInstance);

                var sourceFiles = GetFullPaths(
                    projectInstance.GetItems(ItemNames.Compile), filter: FileNameIsNotGenerated);

                var projectReferences = ImmutableArray.CreateBuilder<string>();
                var projectReferenceAliases = ImmutableDictionary.CreateBuilder<string, string>();
                var projectReferencesAdded = new HashSet<string>();
                foreach (var projectReferenceItem in projectInstance.GetItems(ItemNames.ProjectReference))
                {
                    var fullPath = projectReferenceItem.GetMetadataValue(MetadataNames.FullPath);

                    if (IsCSharpProject(fullPath) && projectReferencesAdded.Add(fullPath))
                    {
                        projectReferences.Add(fullPath);

                        var aliases = projectReferenceItem.GetMetadataValue(MetadataNames.Aliases);
                        if (!string.IsNullOrEmpty(aliases))
                        {
                            projectReferenceAliases[fullPath] = aliases;
                        }
                    }
                }

                var references = ImmutableArray.CreateBuilder<string>();
                var referenceAliases = ImmutableDictionary.CreateBuilder<string, string>();
                foreach (var referencePathItem in projectInstance.GetItems(ItemNames.ReferencePath))
                {
                    var referenceSourceTarget = referencePathItem.GetMetadataValue(MetadataNames.ReferenceSourceTarget);

                    if (StringComparer.OrdinalIgnoreCase.Equals(referenceSourceTarget, ItemNames.ProjectReference))
                    {
                        // If the reference was sourced from a project reference, we have two choices:
                        //
                        //   1. If the reference is a C# project reference, we shouldn't add it because it'll just duplicate
                        //      the project reference.
                        //   2. If the reference is *not* a C# project reference, we should keep this reference because the
                        //      project reference was already removed.

                        var originalItemSpec = referencePathItem.GetMetadataValue(MetadataNames.OriginalItemSpec);
                        if (originalItemSpec.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    var fullPath = referencePathItem.GetMetadataValue(MetadataNames.FullPath);
                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        references.Add(fullPath);

                        var aliases = referencePathItem.GetMetadataValue(MetadataNames.Aliases);
                        if (!string.IsNullOrEmpty(aliases))
                        {
                            referenceAliases[fullPath] = aliases;
                        }
                    }
                }

                var packageReferences = GetPackageReferences(projectInstance.GetItems(ItemNames.PackageReference));
                var analyzers = GetFullPaths(projectInstance.GetItems(ItemNames.Analyzer));
                var additionalFiles = GetFullPaths(projectInstance.GetItems(ItemNames.AdditionalFiles));

                return new ProjectData(guid, name,
                    assemblyName, targetPath, outputPath, intermediateOutputPath, projectAssetsFile,
                    configuration, platform, targetFramework, targetFrameworks,
                    outputKind, languageVersion, nullableContextOptions, allowUnsafeCode, checkForOverflowUnderflow, documentationFile, preprocessorSymbolNames, suppressedDiagnosticIds,
                    signAssembly, assemblyOriginatorKeyFile,
                    sourceFiles, projectReferences.ToImmutable(), references.ToImmutable(), packageReferences, analyzers, additionalFiles, treatWarningsAsErrors, defaultNamespace, ruleset,
                    referenceAliases.ToImmutableDictionary(), projectReferenceAliases.ToImmutable());
            }

            private static RuleSet ResolveRulesetIfAny(MSB.Execution.ProjectInstance projectInstance)
            {
                var rulesetIfAny = projectInstance.Properties.FirstOrDefault(x => x.Name == "ResolvedCodeAnalysisRuleSet");

                if (rulesetIfAny != null)
                    return RuleSet.LoadEffectiveRuleSetFromFile(Path.Combine(projectInstance.Directory, rulesetIfAny.EvaluatedValue));

                return null;
            }

            private static bool IsCSharpProject(string filePath)
                => filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

            private static bool FileNameIsNotGenerated(string filePath)
                => !Path.GetFileName(filePath).StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase);

            private static ImmutableArray<string> GetFullPaths(IEnumerable<MSB.Execution.ProjectItemInstance> items, Func<string, bool> filter = null)
            {
                var builder = ImmutableArray.CreateBuilder<string>();
                var addedSet = new HashSet<string>();

                filter = filter ?? (_ => true);

                foreach (var item in items)
                {
                    var fullPath = item.GetMetadataValue(MetadataNames.FullPath);

                    if (filter(fullPath) && addedSet.Add(fullPath))
                    {
                        builder.Add(fullPath);
                    }
                }

                return builder.ToImmutable();
            }

            private static ImmutableArray<PackageReference> GetPackageReferences(ICollection<MSB.Execution.ProjectItemInstance> items)
            {
                var builder = ImmutableArray.CreateBuilder<PackageReference>(items.Count);
                var addedSet = new HashSet<PackageReference>();

                foreach (var item in items)
                {
                    var name = item.EvaluatedInclude;
                    var versionValue = item.GetMetadataValue(MetadataNames.Version);
                    var versionRange = PropertyConverter.ToVersionRange(versionValue);
                    var dependency = new PackageDependency(name, versionRange);

                    var isImplicitlyDefinedValue = item.GetMetadataValue(MetadataNames.IsImplicitlyDefined);
                    var isImplicitlyDefined = PropertyConverter.ToBoolean(isImplicitlyDefinedValue, defaultValue: false);

                    var packageReference = new PackageReference(dependency, isImplicitlyDefined);

                    if (addedSet.Add(packageReference))
                    {
                        builder.Add(packageReference);
                    }
                }

                return builder.ToImmutable();
            }
        }
    }
}
