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

namespace OmniSharp.MSBuild.ProjectFile
{
    public partial class ProjectFileInfo
    {
        public ProjectId ProjectId { get; private set; }
        public Guid ProjectGuid { get; }
        public string Name { get; }
        public string ProjectFilePath { get; }
        public FrameworkName TargetFramework { get; }
        public LanguageVersion SpecifiedLanguageVersion { get; }
        public string ProjectDirectory => Path.GetDirectoryName(ProjectFilePath);
        public string AssemblyName { get; }
        public string TargetPath { get; }
        public bool AllowUnsafe { get; }
        public OutputKind OutputKind { get; }
        public bool SignAssembly { get; }
        public string AssemblyOriginatorKeyFile { get; }
        public bool GenerateXmlDocumentation { get; }
        public IList<string> PreprocessorSymbolNames { get; }

        public IList<string> SourceFiles { get; }
        public IList<string> References { get; }
        public IList<string> ProjectReferences { get; }
        public IList<string> Analyzers { get; }

        public ProjectFileInfo(string projectFilePath)
        {
            this.ProjectFilePath = projectFilePath;
        }

        private ProjectFileInfo(
            string projectFilePath,
            string assemblyName,
            string name,
            FrameworkName targetFramework,
            LanguageVersion specifiedLanguageVersion,
            Guid projectGuid,
            string targetPath,
            bool allowUnsafe,
            OutputKind outputKind,
            bool signAssembly,
            string assemblyOriginatorKeyFile,
            bool generateXmlDocumentation,
            IList<string> defineConstants,
            IList<string> sourceFiles,
            IList<string> references,
            IList<string> projectReferences,
            IList<string> analyzers)
        {
            this.ProjectFilePath = projectFilePath;
            this.AssemblyName = assemblyName;
            this.Name = name;
            this.TargetFramework = targetFramework;
            this.SpecifiedLanguageVersion = specifiedLanguageVersion;
            this.ProjectGuid = projectGuid;
            this.TargetPath = targetPath;
            this.AllowUnsafe = allowUnsafe;
            this.OutputKind = outputKind;
            this.SignAssembly = signAssembly;
            this.AssemblyOriginatorKeyFile = assemblyOriginatorKeyFile;
            this.GenerateXmlDocumentation = generateXmlDocumentation;
            this.PreprocessorSymbolNames = defineConstants;
            this.SourceFiles = sourceFiles;
            this.References = references;
            this.ProjectReferences = projectReferences;
            this.Analyzers = analyzers;
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
            MSBuildOptions options,
            ICollection<MSBuildDiagnosticsMessage> diagnostics)
        {
            if (!File.Exists(projectFilePath))
            {
                return null;
            }

#if NET451
            if (PlatformHelper.IsMono)
            {
                return CreateForMono(projectFilePath, solutionDirectory, options, logger, diagnostics);
            }
#endif

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

            if (!string.IsNullOrWhiteSpace(options.VisualStudioVersion))
            {
                globalProperties.Add(PropertyNames.VisualStudioVersion, options.VisualStudioVersion);
            }

            var collection = new ProjectCollection(globalProperties);

            logger.LogInformation($"Using toolset {options.ToolsVersion ?? collection.DefaultToolsVersion} for '{projectFilePath}'");

            var project = string.IsNullOrEmpty(options.ToolsVersion)
                ? collection.LoadProject(projectFilePath)
                : collection.LoadProject(projectFilePath, options.ToolsVersion);

            var projectInstance = project.CreateProjectInstance();
            var buildResult = projectInstance.Build(TargetNames.ResolveReferences,
                new[] { new MSBuildLogForwarder(logger, diagnostics) });

            if (!buildResult)
            {
                return null;
            }

            var assemblyName = projectInstance.GetPropertyValue(PropertyNames.AssemblyName);
            var name = projectInstance.GetPropertyValue(PropertyNames.ProjectName);
            var targetFramework = new FrameworkName(projectInstance.GetPropertyValue(PropertyNames.TargetFrameworkMoniker));
            var specifiedLanguageVersion = PropertyConverter.ToLanguageVersion(projectInstance.GetPropertyValue(PropertyNames.LangVersion));
            var projectGuid = PropertyConverter.ToGuid(projectInstance.GetPropertyValue(PropertyNames.ProjectGuid));
            var targetPath = projectInstance.GetPropertyValue(PropertyNames.TargetPath);
            var allowUnsafe = PropertyConverter.ToBoolean(projectInstance.GetPropertyValue(PropertyNames.AllowUnsafeBlocks), defaultValue: false);
            var outputKind = PropertyConverter.ToOutputKind(projectInstance.GetPropertyValue(PropertyNames.OutputType));
            var signAssembly = PropertyConverter.ToBoolean(projectInstance.GetPropertyValue(PropertyNames.SignAssembly), defaultValue: false);
            var assemblyOriginatorKeyFile = projectInstance.GetPropertyValue(PropertyNames.AssemblyOriginatorKeyFile);
            var documentationFile = projectInstance.GetPropertyValue(PropertyNames.DocumentationFile);
            var defineConstants = PropertyConverter.ToDefineConstants(projectInstance.GetPropertyValue(PropertyNames.DefineConstants));

            var sourceFiles = projectInstance
                .GetItems(ItemNames.Compile)
                .Select(GetFullPath)
                .ToList();

            var references =  projectInstance
                .GetItems(ItemNames.ReferencePath)
                .Where(ReferenceSourceTargetIsProjectReference)
                .Select(GetFullPath)
                .ToList();

            var projectReferences = projectInstance
                .GetItems(ItemNames.ProjectReference)
                .Select(GetFullPath)
                .ToList();

            var analyzers = projectInstance
                .GetItems(ItemNames.Analyzer)
                .Select(GetFullPath)
                .ToList();

            return new ProjectFileInfo(
                projectFilePath, assemblyName, name, targetFramework, specifiedLanguageVersion,
                projectGuid, targetPath, allowUnsafe, outputKind, signAssembly, assemblyOriginatorKeyFile,
                !string.IsNullOrWhiteSpace(documentationFile), defineConstants, sourceFiles, references,
                projectReferences, analyzers);
        }

        private static bool ReferenceSourceTargetIsProjectReference(ProjectItemInstance projectItem)
        {
            return !string.Equals(projectItem.GetMetadataValue(MetadataNames.ReferenceSourceTarget), ItemNames.ProjectReference, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetFullPath(ProjectItemInstance projectItem)
        {
            return projectItem.GetMetadataValue(MetadataNames.FullPath);
        }
    }
}
