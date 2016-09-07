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
        public ProjectId ProjectId { get; set; }
        public Guid ProjectGuid { get; }
        public string Name { get; }
        public string ProjectFilePath { get; }
        public FrameworkName TargetFramework { get; }
        public LanguageVersion? SpecifiedLanguageVersion { get; }
        public string ProjectDirectory => Path.GetDirectoryName(ProjectFilePath);
        public string AssemblyName { get; }
        public string TargetPath { get; }
        public bool AllowUnsafe { get; }
        public OutputKind OutputKind { get; }
        public bool SignAssembly { get; }
        public string AssemblyOriginatorKeyFile { get; }
        public bool GenerateXmlDocumentation { get; }
        public IList<string> DefineConstants { get; }

        public IList<string> SourceFiles { get; }
        public IList<string> References { get; }
        public IList<string> ProjectReferences { get; }
        public IList<string> Analyzers { get; }

        public ProjectFileInfo()
        {
        }

        private ProjectFileInfo(
            string projectFilePath,
            string assemblyName,
            string name,
            FrameworkName targetFramework,
            LanguageVersion? specifiedLanguageVersion,
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
            this.DefineConstants = defineConstants;
            this.SourceFiles = sourceFiles;
            this.References = references;
            this.ProjectReferences = projectReferences;
            this.Analyzers = analyzers;
        }

        public static ProjectFileInfo Create(
            string projectFilePath,
            string solutionDirectory,
            ILogger logger,
            MSBuildOptions options,
            ICollection<MSBuildDiagnosticsMessage> diagnostics)
        {
#if NET451
            if (PlatformHelper.IsMono)
            {
                return CreateForMono(projectFilePath, solutionDirectory, options, logger, diagnostics);
            }
#endif

            var globalProperties = new Dictionary<string, string>
            {
                { WellKnownPropertyNames.DesignTimeBuild, "true" },
                { WellKnownPropertyNames.BuildProjectReferences, "false" },
                { WellKnownPropertyNames._ResolveReferenceDependencies, "true" },
                { WellKnownPropertyNames.SolutionDir, solutionDirectory + Path.DirectorySeparatorChar }
            };

            if (!string.IsNullOrWhiteSpace(options.VisualStudioVersion))
            {
                globalProperties.Add(WellKnownPropertyNames.VisualStudioVersion, options.VisualStudioVersion);
            }

            var collection = new ProjectCollection(globalProperties);

            logger.LogInformation("Using toolset {0} for '{1}'", options.ToolsVersion ?? collection.DefaultToolsVersion, projectFilePath);

            var project = string.IsNullOrEmpty(options.ToolsVersion)
                ? collection.LoadProject(projectFilePath)
                : collection.LoadProject(projectFilePath, options.ToolsVersion);

            var projectInstance = project.CreateProjectInstance();
            var buildResult = projectInstance.Build(WellKnownTargetNames.ResolveReferences,
                new[] { new MSBuildLogForwarder(logger, diagnostics) });

            if (!buildResult)
            {
                return null;
            }

            var assemblyName = projectInstance.GetPropertyValue(WellKnownPropertyNames.AssemblyName);
            var name = projectInstance.GetPropertyValue(WellKnownPropertyNames.ProjectName);
            var targetFramework = new FrameworkName(projectInstance.GetPropertyValue(WellKnownPropertyNames.TargetFrameworkMoniker));
            var specifiedLanguageVersion = PropertyConverter.ToLanguageVersion(projectInstance.GetPropertyValue(WellKnownPropertyNames.LangVersion));
            var projectGuid = PropertyConverter.ToGuid(projectInstance.GetPropertyValue(WellKnownPropertyNames.ProjectGuid));
            var targetPath = projectInstance.GetPropertyValue(WellKnownPropertyNames.TargetPath);
            var allowUnsafe = PropertyConverter.ToBoolean(projectInstance.GetPropertyValue(WellKnownPropertyNames.AllowUnsafeBlocks));
            var outputKind = PropertyConverter.ToOutputKind(projectInstance.GetPropertyValue(WellKnownPropertyNames.OutputType));
            var signAssembly = PropertyConverter.ToBoolean(projectInstance.GetPropertyValue(WellKnownPropertyNames.SignAssembly));
            var assemblyOriginatorKeyFile = projectInstance.GetPropertyValue(WellKnownPropertyNames.AssemblyOriginatorKeyFile);
            var documentationFile = projectInstance.GetPropertyValue(WellKnownPropertyNames.DocumentationFile);
            var defineConstants = PropertyConverter.ToDefineConstants(projectInstance.GetPropertyValue(WellKnownPropertyNames.DefineConstants));

            var sourceFiles = projectInstance
                .GetItems(WellKnownItemNames.Compile)
                .Select(GetFullPath)
                .ToList();

            var references =  projectInstance
                .GetItems(WellKnownItemNames.ReferencePath)
                .Where(ReferenceSourceTargetIsProjectReference)
                .Select(GetFullPath)
                .ToList();

            var projectReferences = projectInstance
                .GetItems(WellKnownItemNames.ProjectReference)
                .Select(GetFullPath)
                .ToList();

            var analyzers = projectInstance
                .GetItems(WellKnownItemNames.Analyzer)
                .Select(GetFullPath)
                .ToList();

            return new ProjectFileInfo(
                projectFilePath,
                assemblyName,
                name,
                targetFramework,
                specifiedLanguageVersion,
                projectGuid,
                targetPath,
                allowUnsafe ?? false,
                outputKind,
                signAssembly ?? false,
                assemblyOriginatorKeyFile,
                !string.IsNullOrWhiteSpace(documentationFile),
                defineConstants,
                sourceFiles,
                references,
                projectReferences,
                analyzers);
        }

        private static bool ReferenceSourceTargetIsProjectReference(ProjectItemInstance projectItem)
        {
            return !string.Equals(projectItem.GetMetadataValue(WellKnownMetadataNames.ReferenceSourceTarget), WellKnownItemNames.ProjectReference, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetFullPath(ProjectItemInstance projectItem)
        {
            return projectItem.GetMetadataValue(WellKnownMetadataNames.FullPath);
        }
    }
}
