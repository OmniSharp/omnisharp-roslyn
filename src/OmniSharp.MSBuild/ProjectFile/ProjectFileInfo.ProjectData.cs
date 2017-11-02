using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuGet.Packaging.Core;

namespace OmniSharp.MSBuild.ProjectFile
{
    public partial class ProjectFileInfo
    {
        private class ProjectData
        {
            public Guid Guid { get; }
            public string Name { get; }

            public string AssemblyName { get; }
            public string TargetPath { get; }
            public string OutputPath { get;  }
            public string ProjectAssetsFile { get; }

            public FrameworkName TargetFramework { get; }
            public ImmutableArray<string> TargetFrameworks { get; }

            public OutputKind OutputKind { get; }
            public LanguageVersion LanguageVersion { get; }
            public bool AllowUnsafeCode { get; }
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

            private ProjectData(
                Guid guid, string name,
                string assemblyName, string targetPath, string outputPath, string projectAssetsFile,
                FrameworkName targetFramework,
                ImmutableArray<string> targetFrameworks,
                OutputKind outputKind,
                LanguageVersion languageVersion,
                bool allowUnsafeCode,
                string documentationFile,
                ImmutableArray<string> preprocessorSymbolNames,
                ImmutableArray<string> suppressedDiagnosticIds,
                bool signAssembly,
                string assemblyOriginatorKeyFile,
                ImmutableArray<string> sourceFiles,
                ImmutableArray<string> projectReferences,
                ImmutableArray<string> references,
                ImmutableArray<PackageReference> packageReferences,
                ImmutableArray<string> analyzers)
            {
                Guid = guid;
                Name = name;

                AssemblyName = assemblyName;
                TargetPath = targetPath;
                OutputPath = outputPath;
                ProjectAssetsFile = projectAssetsFile;

                TargetFramework = targetFramework;
                TargetFrameworks = targetFrameworks;

                OutputKind = outputKind;
                LanguageVersion = languageVersion;
                AllowUnsafeCode = allowUnsafeCode;
                DocumentationFile = documentationFile;
                PreprocessorSymbolNames = preprocessorSymbolNames;
                SuppressedDiagnosticIds = suppressedDiagnosticIds;

                SignAssembly = signAssembly;
                AssemblyOriginatorKeyFile = assemblyOriginatorKeyFile;

                SourceFiles = sourceFiles;
                ProjectReferences = projectReferences;
                References = references;
                PackageReferences = packageReferences;
                Analyzers = analyzers;
            }

            public static ProjectData Create(ProjectInstance projectInstance)
            {
                var guid = PropertyConverter.ToGuid(projectInstance.GetPropertyValue(PropertyNames.ProjectGuid));
                var name = projectInstance.GetPropertyValue(PropertyNames.ProjectName);
                var assemblyName = projectInstance.GetPropertyValue(PropertyNames.AssemblyName);
                var targetPath = projectInstance.GetPropertyValue(PropertyNames.TargetPath);
                var outputPath = projectInstance.GetPropertyValue(PropertyNames.OutputPath);
                var projectAssetsFile = projectInstance.GetPropertyValue(PropertyNames.ProjectAssetsFile);

                var targetFramework = new FrameworkName(projectInstance.GetPropertyValue(PropertyNames.TargetFrameworkMoniker));

                var targetFrameworkValue = projectInstance.GetPropertyValue(PropertyNames.TargetFramework);
                var targetFrameworks = PropertyConverter.SplitList(projectInstance.GetPropertyValue(PropertyNames.TargetFrameworks), ';');

                if (!string.IsNullOrWhiteSpace(targetFrameworkValue) && targetFrameworks.Length == 0)
                {
                    targetFrameworks = ImmutableArray.Create(targetFrameworkValue);
                }

                var languageVersion = PropertyConverter.ToLanguageVersion(projectInstance.GetPropertyValue(PropertyNames.LangVersion));
                var allowUnsafeCode = PropertyConverter.ToBoolean(projectInstance.GetPropertyValue(PropertyNames.AllowUnsafeBlocks), defaultValue: false);
                var outputKind = PropertyConverter.ToOutputKind(projectInstance.GetPropertyValue(PropertyNames.OutputType));
                var documentationFile = projectInstance.GetPropertyValue(PropertyNames.DocumentationFile);
                var preprocessorSymbolNames = PropertyConverter.ToPreprocessorSymbolNames(projectInstance.GetPropertyValue(PropertyNames.DefineConstants));
                var suppressDiagnosticIds = PropertyConverter.ToSuppressDiagnosticIds(projectInstance.GetPropertyValue(PropertyNames.NoWarn));
                var signAssembly = PropertyConverter.ToBoolean(projectInstance.GetPropertyValue(PropertyNames.SignAssembly), defaultValue: false);
                var assemblyOriginatorKeyFile = projectInstance.GetPropertyValue(PropertyNames.AssemblyOriginatorKeyFile);

                var sourceFiles = GetFullPaths(
                    projectInstance.GetItems(ItemNames.Compile), filter: FileNameIsNotGenerated);
                var projectReferences = GetFullPaths(projectInstance.GetItems(ItemNames.ProjectReference));
                var references = GetFullPaths(
                    projectInstance.GetItems(ItemNames.ReferencePath).Where(ReferenceSourceTargetIsNotProjectReference));
                var packageReferences = GetPackageReferences(projectInstance.GetItems(ItemNames.PackageReference));
                var analyzers = GetFullPaths(projectInstance.GetItems(ItemNames.Analyzer));

                return new ProjectData(guid, name,
                    assemblyName, targetPath, outputPath, projectAssetsFile,
                    targetFramework, targetFrameworks,
                    outputKind, languageVersion, allowUnsafeCode, documentationFile, preprocessorSymbolNames, suppressDiagnosticIds,
                    signAssembly, assemblyOriginatorKeyFile,
                    sourceFiles, projectReferences, references, packageReferences, analyzers);
            }

            private static bool ReferenceSourceTargetIsNotProjectReference(ProjectItemInstance item)
                => item.GetMetadataValue(MetadataNames.ReferenceSourceTarget) != ItemNames.ProjectReference;

            private static bool FileNameIsNotGenerated(string filePath)
                => !Path.GetFileName(filePath).StartsWith("TemporaryGeneratedFile_");

            private static ImmutableArray<string> GetFullPaths(IEnumerable<ProjectItemInstance> items, Func<string, bool> filter = null)
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

            private static ImmutableArray<PackageReference> GetPackageReferences(ICollection<ProjectItemInstance> items)
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
