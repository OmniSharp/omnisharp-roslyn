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
        public string AssemblyOriginatorKeyFile => _data.AssemblyName;

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
            string filePath, string solutionDirectory, string sdksPath, ILogger logger,
            MSBuildOptions options = null, ICollection<MSBuildDiagnosticsMessage> diagnostics = null)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var projectInstance = LoadProject(filePath, solutionDirectory, sdksPath, logger, options, diagnostics, out var targetFrameworks);
            if (projectInstance == null)
            {
                return null;
            }

            var id = ProjectId.CreateNewId(debugName: filePath);
            var data = CreateProjectData(projectInstance, targetFrameworks);

            return new ProjectFileInfo(id, filePath, data);
        }

        private static ProjectInstance LoadProject(
            string filePath, string solutionDirectory, string sdksPath, ILogger logger,
            MSBuildOptions options, ICollection<MSBuildDiagnosticsMessage> diagnostics, out ImmutableArray<string> targetFrameworks)
        {
            options = options ?? new MSBuildOptions();

            var globalProperties = GetGlobalProperties(options, solutionDirectory, sdksPath, logger);

            const string MSBuildSDKsPath = "MSBuildSDKsPath";
            var oldSdksPathValue = Environment.GetEnvironmentVariable(MSBuildSDKsPath);
            try
            {
                if (globalProperties.TryGetValue(MSBuildSDKsPath, out var sdksPathValue))
                {
                    Environment.SetEnvironmentVariable(MSBuildSDKsPath, sdksPathValue);
                }

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
                var buildResult = projectInstance.Build(TargetNames.Compile,
                    new[] { new MSBuildLogForwarder(logger, diagnostics) });

                return buildResult
                    ? projectInstance
                    : null;
            }
            finally
            {
                Environment.SetEnvironmentVariable(MSBuildSDKsPath, oldSdksPathValue);
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

            var sourceFiles = GetFullPaths(projectInstance.GetItems(ItemNames.Compile));
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

        public ProjectFileInfo Reload(
            string solutionDirectory, string sdksPath, ILogger logger,
            MSBuildOptions options = null, ICollection<MSBuildDiagnosticsMessage> diagnostics = null)
        {
            var projectInstance = LoadProject(FilePath, solutionDirectory, sdksPath, logger, options, diagnostics, out var targetFrameworks);
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

        private static Dictionary<string, string> GetGlobalProperties(MSBuildOptions options, string solutionDirectory, string sdksPath, ILogger logger)
        {
            var globalProperties = new Dictionary<string, string>
            {
                { PropertyNames.DesignTimeBuild, "true" },
                { PropertyNames.BuildProjectReferences, "false" },
                { PropertyNames._ResolveReferenceDependencies, "true" },
                { PropertyNames.SolutionDir, solutionDirectory + Path.DirectorySeparatorChar },

                // This properties allow the design-time build to handle the Compile target without actually invoking the compiler.
                // See https://github.com/dotnet/roslyn/pull/4604 for details.
                { PropertyNames.ProvideCommandLineInvocation, "true" },
                { PropertyNames.SkipCompilerExecution, "true" }
            };

            globalProperties.AddPropertyIfNeeded(
                logger,
                PropertyNames.MSBuildExtensionsPath,
                userOptionValue: options.MSBuildExtensionsPath,
                environmentValue: MSBuildEnvironment.MSBuildExtensionsPath);

            globalProperties.AddPropertyIfNeeded(
                logger,
                PropertyNames.MSBuildSDKsPath,
                userOptionValue: options.MSBuildSDKsPath,
                environmentValue: sdksPath);

            globalProperties.AddPropertyIfNeeded(
                logger,
                PropertyNames.TargetFrameworkRootPath,
                userOptionValue: options.TargetFrameworkRootPath,
                environmentValue: MSBuildEnvironment.TargetFrameworkRootPath);

            globalProperties.AddPropertyIfNeeded(
                logger,
                PropertyNames.RoslynTargetsPath,
                userOptionValue: options.RoslynTargetsPath,
                environmentValue: MSBuildEnvironment.RoslynTargetsPath);

            globalProperties.AddPropertyIfNeeded(
                logger,
                PropertyNames.CscToolPath,
                userOptionValue: options.CscToolPath,
                environmentValue: MSBuildEnvironment.CscToolPath);

            globalProperties.AddPropertyIfNeeded(
                logger,
                PropertyNames.CscToolExe,
                userOptionValue: options.CscToolExe,
                environmentValue: MSBuildEnvironment.CscToolExe);

            globalProperties.AddPropertyIfNeeded(
                logger,
                PropertyNames.VisualStudioVersion,
                userOptionValue: options.VisualStudioVersion,
                environmentValue: null);

            globalProperties.AddPropertyIfNeeded(
                logger,
                PropertyNames.Configuration,
                userOptionValue: options.Configuration,
                environmentValue: null);

            globalProperties.AddPropertyIfNeeded(
                logger,
                PropertyNames.Platform,
                userOptionValue: options.Platform,
                environmentValue: null);

            return globalProperties;
        }

        private static bool ReferenceSourceTargetIsNotProjectReference(ProjectItemInstance item)
        {
            return item.GetMetadataValue(MetadataNames.ReferenceSourceTarget) != ItemNames.ProjectReference;
        }

        private static ImmutableArray<string> GetFullPaths(IEnumerable<ProjectItemInstance> items)
        {
            var builder = ImmutableArray.CreateBuilder<string>();
            var addedSet = new HashSet<string>();

            foreach (var item in items)
            {
                var fullPath = item.GetMetadataValue(MetadataNames.FullPath);

                if (addedSet.Add(fullPath))
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
