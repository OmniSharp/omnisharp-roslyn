using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild.ProjectFile
{
    public partial class ProjectFileInfo
    {
        public ProjectId ProjectId { get; private set; }
        public Guid ProjectGuid { get; }
        public string Name { get; }
        public string ProjectFilePath { get; }
        public bool IsUnityProject { get; }
        public FrameworkName TargetFramework { get; }
        public IList<string> TargetFrameworks { get; }
        public LanguageVersion SpecifiedLanguageVersion { get; }
        public string ProjectDirectory => Path.GetDirectoryName(ProjectFilePath);
        public string AssemblyName { get; }
        public string TargetPath { get; }
        public bool AllowUnsafe { get; }
        public OutputKind OutputKind { get; }
        public bool SignAssembly { get; }
        public string AssemblyOriginatorKeyFile { get; }
        public bool GenerateXmlDocumentation { get; }
        public string OutputPath { get; }
        public string ProjectAssetsFile { get; }
        public IList<string> PreprocessorSymbolNames { get; }
        public IList<string> SuppressedDiagnosticIds { get; }

        public IList<string> SourceFiles { get; }
        public IList<string> References { get; }
        public IList<string> ProjectReferences { get; }
        public IList<string> Analyzers { get; }
        public IList<PackageReference> PackageReferences { get; }

        public ProjectFileInfo(string projectFilePath)
        {
            this.ProjectFilePath = projectFilePath;
        }

        private ProjectFileInfo(
            string projectFilePath,
            string assemblyName,
            string name,
            FrameworkName targetFramework,
            IList<string> targetFrameworks,
            LanguageVersion specifiedLanguageVersion,
            Guid projectGuid,
            string targetPath,
            bool allowUnsafe,
            OutputKind outputKind,
            bool signAssembly,
            string assemblyOriginatorKeyFile,
            bool generateXmlDocumentation,
            string outputPath,
            string projectAssetsFile,
            bool isUnityProject,
            IList<string> defineConstants,
            IList<string> suppressedDiagnosticIds,
            IList<string> sourceFiles,
            IList<string> references,
            IList<string> projectReferences,
            IList<string> analyzers,
            IList<PackageReference> packageReferences)
        {
            this.ProjectFilePath = projectFilePath;
            this.AssemblyName = assemblyName;
            this.Name = name;
            this.TargetFramework = targetFramework;
            this.TargetFrameworks = targetFrameworks;
            this.SpecifiedLanguageVersion = specifiedLanguageVersion;
            this.ProjectGuid = projectGuid;
            this.TargetPath = targetPath;
            this.AllowUnsafe = allowUnsafe;
            this.OutputKind = outputKind;
            this.SignAssembly = signAssembly;
            this.AssemblyOriginatorKeyFile = assemblyOriginatorKeyFile;
            this.GenerateXmlDocumentation = generateXmlDocumentation;
            this.OutputPath = outputPath;
            this.ProjectAssetsFile = projectAssetsFile;
            this.IsUnityProject = isUnityProject;
            this.PreprocessorSymbolNames = defineConstants;
            this.SuppressedDiagnosticIds = suppressedDiagnosticIds;
            this.SourceFiles = sourceFiles;
            this.References = references;
            this.ProjectReferences = projectReferences;
            this.Analyzers = analyzers;
            this.PackageReferences = packageReferences;
        }

        public void SetProjectId(ProjectId projectId)
        {
            if (this.ProjectId != null)
            {
                throw new ArgumentException("ProjectId is already set!", nameof(projectId));
            }

            this.ProjectId = projectId;
        }

        public static ProjectFileInfo Create(
            string projectFilePath,
            string solutionDirectory,
            ILogger logger,
            MSBuildOptions options = null,
            ICollection<MSBuildDiagnosticsMessage> diagnostics = null,
            bool isUnityProject = false)
        {
            if (!File.Exists(projectFilePath))
            {
                return null;
            }

            options = options ?? new MSBuildOptions();

            var globalProperties = new Dictionary<string, string>
            {
                { PropertyNames.DesignTimeBuild, "true" },
                { PropertyNames.BuildProjectReferences, "false" },
                { PropertyNames._ResolveReferenceDependencies, "true" },
                { PropertyNames.SolutionDir, solutionDirectory + Path.DirectorySeparatorChar }
            };

            if (!string.IsNullOrWhiteSpace(options.MSBuildExtensionsPath))
            {
                globalProperties.Add(PropertyNames.MSBuildExtensionsPath, options.MSBuildExtensionsPath);
            }
            else if (!string.IsNullOrWhiteSpace(MSBuildEnvironment.MSBuildExtensionsPath))
            {
                globalProperties.Add(PropertyNames.MSBuildExtensionsPath, MSBuildEnvironment.MSBuildExtensionsPath);
            }

            if (!string.IsNullOrWhiteSpace(options.MSBuildSDKsPath))
            {
                globalProperties.Add(PropertyNames.MSBuildSDKsPath, options.MSBuildSDKsPath);
            }
            else if (!string.IsNullOrWhiteSpace(MSBuildEnvironment.MSBuildSDKsPath))
            {
                globalProperties.Add(PropertyNames.MSBuildSDKsPath, MSBuildEnvironment.MSBuildSDKsPath);
            }

            if (PlatformHelper.IsMono)
            {
                var monoXBuildFrameworksDirPath = PlatformHelper.MonoXBuildFrameworksDirPath;
                if (monoXBuildFrameworksDirPath != null)
                {
                    logger.LogDebug($"Using TargetFrameworkRootPath: {monoXBuildFrameworksDirPath}");
                    globalProperties.Add(PropertyNames.TargetFrameworkRootPath, monoXBuildFrameworksDirPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(options.VisualStudioVersion))
            {
                globalProperties.Add(PropertyNames.VisualStudioVersion, options.VisualStudioVersion);
            }

            var collection = new ProjectCollection(globalProperties);

            // Evaluate the MSBuild project
            var project = string.IsNullOrEmpty(options.ToolsVersion)
                ? collection.LoadProject(projectFilePath)
                : collection.LoadProject(projectFilePath, options.ToolsVersion);

            var targetFramework = project.GetPropertyValue(PropertyNames.TargetFramework);
            var targetFrameworks = PropertyConverter.ToList(project.GetPropertyValue(PropertyNames.TargetFrameworks), ';');

            // If the project supports multiple target frameworks and specific framework isn't
            // selected, we must pick one before execution. Otherwise, the ResolveReferences
            // target might not be available to us.
            if (string.IsNullOrWhiteSpace(targetFramework) && targetFrameworks.Count > 0)
            {
                // For now, we'll just pick the first target framework. Eventually, we'll need to
                // do better and potentially allow OmniSharp hosts to select a target framework.
                targetFramework = targetFrameworks[0];
                project.SetProperty(PropertyNames.TargetFramework, targetFramework);
            }
            else if (!string.IsNullOrWhiteSpace(targetFramework) && targetFrameworks.Count == 0)
            {
                targetFrameworks = new[] { targetFramework };
            }

            var projectInstance = project.CreateProjectInstance();
            var buildResult = projectInstance.Build(TargetNames.ResolveReferences,
                new[] { new MSBuildLogForwarder(logger, diagnostics) });

            if (!buildResult)
            {
                return null;
            }

            var assemblyName = projectInstance.GetPropertyValue(PropertyNames.AssemblyName);
            var name = projectInstance.GetPropertyValue(PropertyNames.ProjectName);

            var targetFrameworkMoniker = projectInstance.GetPropertyValue(PropertyNames.TargetFrameworkMoniker);

            var specifiedLanguageVersion = PropertyConverter.ToLanguageVersion(projectInstance.GetPropertyValue(PropertyNames.LangVersion));
            var projectGuid = PropertyConverter.ToGuid(projectInstance.GetPropertyValue(PropertyNames.ProjectGuid));
            var targetPath = projectInstance.GetPropertyValue(PropertyNames.TargetPath);
            var allowUnsafe = PropertyConverter.ToBoolean(projectInstance.GetPropertyValue(PropertyNames.AllowUnsafeBlocks), defaultValue: false);
            var outputKind = PropertyConverter.ToOutputKind(projectInstance.GetPropertyValue(PropertyNames.OutputType));
            var signAssembly = PropertyConverter.ToBoolean(projectInstance.GetPropertyValue(PropertyNames.SignAssembly), defaultValue: false);
            var assemblyOriginatorKeyFile = projectInstance.GetPropertyValue(PropertyNames.AssemblyOriginatorKeyFile);
            var documentationFile = projectInstance.GetPropertyValue(PropertyNames.DocumentationFile);
            var defineConstants = PropertyConverter.ToDefineConstants(projectInstance.GetPropertyValue(PropertyNames.DefineConstants));
            var noWarn = PropertyConverter.ToSuppressDiagnostics(projectInstance.GetPropertyValue(PropertyNames.NoWarn));
            var outputPath = projectInstance.GetPropertyValue(PropertyNames.OutputPath);
            var projectAssetsFile = projectInstance.GetPropertyValue(PropertyNames.ProjectAssetsFile);

            var sourceFiles = GetFullPaths(projectInstance.GetItems(ItemNames.Compile));
            var references = GetFullPaths(projectInstance.GetItems(ItemNames.ReferencePath));
            var projectReferences = GetFullPaths(projectInstance.GetItems(ItemNames.ProjectReference));
            var analyzers = GetFullPaths(projectInstance.GetItems(ItemNames.Analyzer));

            var packageReferences = GetPackageReferences(projectInstance.GetItems(ItemNames.PackageReference));

            return new ProjectFileInfo(
                projectFilePath, assemblyName, name, new FrameworkName(targetFrameworkMoniker), targetFrameworks, specifiedLanguageVersion,
                projectGuid, targetPath, allowUnsafe, outputKind, signAssembly, assemblyOriginatorKeyFile,
                !string.IsNullOrWhiteSpace(documentationFile), outputPath, projectAssetsFile, isUnityProject, defineConstants, noWarn,
                sourceFiles, references, projectReferences, analyzers, packageReferences);
        }

        private static IList<string> GetFullPaths(ICollection<ProjectItemInstance> items)
        {
            var sortedSet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                sortedSet.Add(item.GetMetadataValue(MetadataNames.FullPath));
            }

            return sortedSet.ToList();
        }

        private static IList<PackageReference> GetPackageReferences(ICollection<ProjectItemInstance> items)
        {
            var list = new List<PackageReference>(items.Count);

            foreach (var item in items)
            {
                var name = item.EvaluatedInclude;
                var version = PropertyConverter.ToNuGetVersion(item.GetMetadataValue(MetadataNames.Version));
                var isImplicitlyDefined = PropertyConverter.ToBoolean(item.GetMetadataValue(MetadataNames.IsImplicitlyDefined), false);

                list.Add(new PackageReference(name, version, isImplicitlyDefined));
            }

            return list;
        }
    }
}
