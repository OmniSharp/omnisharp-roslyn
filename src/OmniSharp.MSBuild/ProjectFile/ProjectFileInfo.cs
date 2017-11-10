using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using NuGet.Packaging.Core;
using OmniSharp.MSBuild.Discovery;
using OmniSharp.MSBuild.Models.Events;
using OmniSharp.Options;

namespace OmniSharp.MSBuild.ProjectFile
{
    public partial class ProjectFileInfo
    {
        private readonly ProjectData _data;

        public string FilePath { get; }
        public string Directory { get; }

        public ProjectId Id { get; }

        public Guid Guid => _data.Guid;
        public string Name => _data.Name;

        public string AssemblyName => _data.AssemblyName;
        public string TargetPath => _data.TargetPath;
        public string OutputPath => _data.OutputPath;
        public string ProjectAssetsFile => _data.ProjectAssetsFile;

        public FrameworkName TargetFramework => _data.TargetFramework;
        public ImmutableArray<string> TargetFrameworks => _data.TargetFrameworks;

        public OutputKind OutputKind => _data.OutputKind;
        public LanguageVersion LanguageVersion => _data.LanguageVersion;
        public bool AllowUnsafeCode => _data.AllowUnsafeCode;
        public string DocumentationFile => _data.DocumentationFile;
        public IList<string> PreprocessorSymbolNames => _data.PreprocessorSymbolNames;
        public IList<string> SuppressedDiagnosticIds => _data.SuppressedDiagnosticIds;

        public bool SignAssembly => _data.SignAssembly;
        public string AssemblyOriginatorKeyFile => _data.AssemblyOriginatorKeyFile;

        public ImmutableArray<string> SourceFiles => _data.SourceFiles;
        public ImmutableArray<string> References => _data.References;
        public ImmutableArray<string> ProjectReferences => _data.ProjectReferences;
        public ImmutableArray<PackageReference> PackageReferences => _data.PackageReferences;
        public ImmutableArray<string> Analyzers => _data.Analyzers;

        internal ProjectFileInfo(string filePath)
        {
            this.FilePath = filePath;
        }

        private ProjectFileInfo(
            ProjectId id,
            string filePath,
            ProjectData data)
        {
            this.Id = id;
            this.FilePath = filePath;
            this.Directory = Path.GetDirectoryName(filePath);

            _data = data;
        }

        public static ProjectFileInfo Create(
            string filePath, string solutionDirectory, ILogger logger,
            MSBuildInstance msbuildInstance, SdksPathResolver sdksPathResolver, MSBuildOptions options = null, ICollection<MSBuildDiagnosticsMessage> diagnostics = null)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var projectInstance = LoadProject(filePath, solutionDirectory, logger, msbuildInstance, sdksPathResolver, options, diagnostics, out var targetFrameworks);
            if (projectInstance == null)
            {
                return null;
            }

            var id = ProjectId.CreateNewId(debugName: filePath);
            var data = CreateProjectData(projectInstance, targetFrameworks);

            return new ProjectFileInfo(id, filePath, data);
        }

        private static ProjectInstance LoadProject(
            string filePath, string solutionDirectory, ILogger logger,
            MSBuildInstance msbuildInstance, SdksPathResolver sdksPathResolver, MSBuildOptions options, ICollection<MSBuildDiagnosticsMessage> diagnostics, out ImmutableArray<string> targetFrameworks)
        {
            using (sdksPathResolver.SetSdksPathEnvironmentVariable(filePath))
            {
                options = options ?? new MSBuildOptions();

                var globalProperties = GetGlobalProperties(msbuildInstance, options, solutionDirectory, logger);

                var collection = new ProjectCollection(globalProperties);

                var toolsVersion = options.ToolsVersion;
                if (string.IsNullOrEmpty(toolsVersion) || Version.TryParse(toolsVersion, out _))
                {
                    toolsVersion = collection.DefaultToolsVersion;
                }

                toolsVersion = GetLegalToolsetVersion(toolsVersion, collection.Toolsets);

                // Evaluate the MSBuild project
                var project = collection.LoadProject(filePath, toolsVersion);

                var targetFramework = project.GetPropertyValue(PropertyNames.TargetFramework);
                targetFrameworks = PropertyConverter.SplitList(project.GetPropertyValue(PropertyNames.TargetFrameworks), ';');

                // If the project supports multiple target frameworks and specific framework isn't
                // selected, we must pick one before execution. Otherwise, the ResolveReferences
                // target might not be available to us.
                if (string.IsNullOrWhiteSpace(targetFramework) && targetFrameworks.Length > 0)
                {
                    // For now, we'll just pick the first target framework. Eventually, we'll need to
                    // do better and potentially allow OmniSharp hosts to select a target framework.
                    targetFramework = targetFrameworks[0];
                    project.SetProperty(PropertyNames.TargetFramework, targetFramework);
                }
                else if (!string.IsNullOrWhiteSpace(targetFramework) && targetFrameworks.Length == 0)
                {
                    targetFrameworks = ImmutableArray.Create(targetFramework);
                }

                var projectInstance = project.CreateProjectInstance();
                var buildResult = projectInstance.Build(new string[] { TargetNames.Compile, TargetNames.CoreCompile },
                    new[] { new MSBuildLogForwarder(logger, diagnostics) });

                return buildResult
                    ? projectInstance
                    : null;
            }
        }

        private static string GetLegalToolsetVersion(string toolsVersion, ICollection<Toolset> toolsets)
        {
            // It's entirely possible the the toolset specified does not exist. In that case, we'll try to use
            // the highest version available.
            var version = new Version(toolsVersion);

            bool exists = false;
            Version highestVersion = null;

            var legalToolsets = new SortedList<Version, Toolset>(toolsets.Count);
            foreach (var toolset in toolsets)
            {
                // Only consider this toolset if it has a legal version, we haven't seen it, and its path exists.
                if (Version.TryParse(toolset.ToolsVersion, out var toolsetVersion) &&
                    !legalToolsets.ContainsKey(toolsetVersion) &&
                    System.IO.Directory.Exists(toolset.ToolsPath))
                {
                    legalToolsets.Add(toolsetVersion, toolset);

                    if (highestVersion == null ||
                        toolsetVersion > highestVersion)
                    {
                        highestVersion = toolsetVersion;
                    }

                    if (toolsetVersion == version)
                    {
                        exists = true;
                    }
                }
            }

            if (highestVersion == null)
            {
                throw new InvalidOperationException("No legal MSBuild toolsets available.");
            }

            if (!exists)
            {
                toolsVersion = legalToolsets[highestVersion].ToolsPath;
            }

            return toolsVersion;
        }

        private static ProjectData CreateProjectData(ProjectInstance projectInstance, ImmutableArray<string> targetFrameworks)
        {
            var guid = PropertyConverter.ToGuid(projectInstance.GetPropertyValue(PropertyNames.ProjectGuid));
            var name = projectInstance.GetPropertyValue(PropertyNames.ProjectName);
            var assemblyName = projectInstance.GetPropertyValue(PropertyNames.AssemblyName);
            var targetPath = projectInstance.GetPropertyValue(PropertyNames.TargetPath);
            var outputPath = projectInstance.GetPropertyValue(PropertyNames.OutputPath);
            var projectAssetsFile = projectInstance.GetPropertyValue(PropertyNames.ProjectAssetsFile);

            var targetFramework = new FrameworkName(projectInstance.GetPropertyValue(PropertyNames.TargetFrameworkMoniker));

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

            var projectReferences = GetFullPaths(
                projectInstance.GetItems(ItemNames.ProjectReference), filter: IsCSharpProject);

            var references = ImmutableArray.CreateBuilder<string>();
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
                }
            }

            var packageReferences = GetPackageReferences(projectInstance.GetItems(ItemNames.PackageReference));
            var analyzers = GetFullPaths(projectInstance.GetItems(ItemNames.Analyzer));

            return new ProjectData(guid, name,
                assemblyName, targetPath, outputPath, projectAssetsFile,
                targetFramework, targetFrameworks,
                outputKind, languageVersion, allowUnsafeCode, documentationFile, preprocessorSymbolNames, suppressDiagnosticIds,
                signAssembly, assemblyOriginatorKeyFile,
                sourceFiles, projectReferences, references.ToImmutable(), packageReferences, analyzers);
        }

        public ProjectFileInfo Reload(
            string solutionDirectory, ILogger logger,
            MSBuildInstance msbuildInstance, SdksPathResolver sdksPathResolver, MSBuildOptions options = null, ICollection<MSBuildDiagnosticsMessage> diagnostics = null)
        {
            var projectInstance = LoadProject(FilePath, solutionDirectory, logger, msbuildInstance, sdksPathResolver, options, diagnostics, out var targetFrameworks);
            if (projectInstance == null)
            {
                return null;
            }

            var data = CreateProjectData(projectInstance, targetFrameworks);

            return new ProjectFileInfo(Id, FilePath, data);
        }

        public bool IsUnityProject()
        {
            return References.Any(filePath =>
            {
                var fileName = Path.GetFileName(filePath);

                return string.Equals(fileName, "UnityEngine.dll", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fileName, "UnityEditor.dll", StringComparison.OrdinalIgnoreCase);
            });
        }

        private static Dictionary<string, string> GetGlobalProperties(MSBuildInstance msbuildInstance, MSBuildOptions options, string solutionDirectory, ILogger logger)
        {
            var globalProperties = new Dictionary<string, string>
            {
                { PropertyNames.DesignTimeBuild, "true" },
                { PropertyNames.BuildingInsideVisualStudio, "true" },
                { PropertyNames.BuildProjectReferences, "false" },
                { PropertyNames._ResolveReferenceDependencies, "true" },
                { PropertyNames.SolutionDir, solutionDirectory + Path.DirectorySeparatorChar },

                // This properties allow the design-time build to handle the Compile target without actually invoking the compiler.
                // See https://github.com/dotnet/roslyn/pull/4604 for details.
                { PropertyNames.ProvideCommandLineArgs, "true" },
                { PropertyNames.SkipCompilerExecution, "true" }
            };

            globalProperties.AddPropertyOverride(PropertyNames.MSBuildExtensionsPath, options.MSBuildExtensionsPath, msbuildInstance.PropertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.TargetFrameworkRootPath, options.TargetFrameworkRootPath, msbuildInstance.PropertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.RoslynTargetsPath, options.RoslynTargetsPath, msbuildInstance.PropertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.CscToolPath, options.CscToolPath, msbuildInstance.PropertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.CscToolExe, options.CscToolExe, msbuildInstance.PropertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.VisualStudioVersion, options.VisualStudioVersion, msbuildInstance.PropertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.Configuration, options.Configuration, msbuildInstance.PropertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.Platform, options.Platform, msbuildInstance.PropertyOverrides, logger);

            return globalProperties;
        }

        private static bool IsCSharpProject(string filePath)
            => filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

        private static bool FileNameIsNotGenerated(string filePath)
            => !Path.GetFileName(filePath).StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase);

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
