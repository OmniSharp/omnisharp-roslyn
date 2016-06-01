using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
#if NET451
using Microsoft.Build.BuildEngine;
#endif
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using OmniSharp.Models;
using OmniSharp.Options;

namespace OmniSharp.MSBuild.ProjectFile
{
    public class ProjectFileInfo
    {
        public ProjectId WorkspaceId { get; set; }

        public Guid ProjectId { get; private set; }

        public string Name { get; private set; }

        public string ProjectFilePath { get; private set; }

        public FrameworkName TargetFramework { get; private set; }

        public LanguageVersion? SpecifiedLanguageVersion { get; private set; }

        public string ProjectDirectory => Path.GetDirectoryName(ProjectFilePath);

        public string AssemblyName { get; private set; }

        public string TargetPath { get; private set; }

        public IList<string> SourceFiles { get; private set; }

        public IList<string> References { get; private set; }

        public IList<string> ProjectReferences { get; private set; }

        public IList<string> Analyzers { get; private set; }

        public IList<string> DefineConstants { get; private set; }

        public bool AllowUnsafe { get; private set; }

        public OutputKind OutputKind { get; private set; }

        public bool SignAssembly { get; private set; }

        public string AssemblyOriginatorKeyFile { get; private set; }

        public bool GenerateXmlDocumentation { get; private set; }

        public static ProjectFileInfo Create(
            MSBuildOptions options, 
            ILogger logger, 
            string solutionDirectory, 
            string projectFilePath, 
            ICollection<MSBuildDiagnosticsMessage> diagnostics)
        {
            var projectFileInfo = new ProjectFileInfo();
            projectFileInfo.ProjectFilePath = projectFilePath;

            var properties = new Dictionary<string, string>
                {
                    { "DesignTimeBuild", "true" },
                    { "BuildProjectReferences", "false" },
                    { "_ResolveReferenceDependencies", "true" },
                    { "SolutionDir", solutionDirectory + Path.DirectorySeparatorChar }
                };

            if (!string.IsNullOrWhiteSpace(options.VisualStudioVersion))
            {
                properties.Add("VisualStudioVersion", options.VisualStudioVersion);
            }

            var collection = new ProjectCollection(properties);

            logger.LogInformation("Using toolset {0} for {1}", options.ToolsVersion ?? collection.DefaultToolsVersion, projectFilePath);

            var project = string.IsNullOrEmpty(options.ToolsVersion) ?
                    collection.LoadProject(projectFilePath) :
                    collection.LoadProject(projectFilePath, options.ToolsVersion);

            var projectInstance = project.CreateProjectInstance();
            var buildResult = projectInstance.Build("ResolveReferences", new Microsoft.Build.Framework.ILogger[] { new MSBuildLogForwarder(logger, diagnostics) });

            if (!buildResult)
            {
                return null;
            }

            projectFileInfo.AssemblyName = projectInstance.GetPropertyValue("AssemblyName");
            projectFileInfo.Name = projectInstance.GetPropertyValue("ProjectName");
            projectFileInfo.TargetFramework = new FrameworkName(projectInstance.GetPropertyValue("TargetFrameworkMoniker"));
            projectFileInfo.SpecifiedLanguageVersion = ToLanguageVersion(projectInstance.GetPropertyValue("LangVersion"));
            projectFileInfo.ProjectId = new Guid(projectInstance.GetPropertyValue("ProjectGuid").TrimStart('{').TrimEnd('}'));
            projectFileInfo.TargetPath = projectInstance.GetPropertyValue("TargetPath");
            var outputType = projectInstance.GetPropertyValue("OutputType");
            switch (outputType)
            {
                case "Library":
                    projectFileInfo.OutputKind = OutputKind.DynamicallyLinkedLibrary;
                    break;
                case "WinExe":
                    projectFileInfo.OutputKind = OutputKind.WindowsApplication;
                    break;
                default:
                case "Exe":
                    projectFileInfo.OutputKind = OutputKind.ConsoleApplication;
                    break;
            }

            projectFileInfo.SourceFiles =
                projectInstance.GetItems("Compile")
                               .Select(p => p.GetMetadataValue("FullPath"))
                               .ToList();

            projectFileInfo.References =
                projectInstance.GetItems("ReferencePath")
                               .Where(p => !string.Equals("ProjectReference", p.GetMetadataValue("ReferenceSourceTarget"), StringComparison.OrdinalIgnoreCase))
                               .Select(p => p.GetMetadataValue("FullPath"))
                               .ToList();

            projectFileInfo.ProjectReferences =
                projectInstance.GetItems("ProjectReference")
                               .Select(p => p.GetMetadataValue("FullPath"))
                               .ToList();

            projectFileInfo.Analyzers =
                projectInstance.GetItems("Analyzer")
                               .Select(p => p.GetMetadataValue("FullPath"))
                               .ToList();

            var allowUnsafe = projectInstance.GetPropertyValue("AllowUnsafeBlocks");
            if (!string.IsNullOrWhiteSpace(allowUnsafe))
            {
                projectFileInfo.AllowUnsafe = Convert.ToBoolean(allowUnsafe);
            }

            var defineConstants = projectInstance.GetPropertyValue("DefineConstants");
            if (!string.IsNullOrWhiteSpace(defineConstants))
            {
                projectFileInfo.DefineConstants = defineConstants.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
            }

            var signAssembly = projectInstance.GetPropertyValue("SignAssembly");
            if (!string.IsNullOrWhiteSpace(signAssembly))
            {
                projectFileInfo.SignAssembly = Convert.ToBoolean(signAssembly);
            }

            projectFileInfo.AssemblyOriginatorKeyFile = projectInstance.GetPropertyValue("AssemblyOriginatorKeyFile");

            var documentationFile = properties["DocumentationFile"].FinalValue;
            if (!string.IsNullOrWhiteSpace(documentationFile))
            {
                projectFileInfo.GenerateXmlDocumentation = true;
            }
            
            return projectFileInfo;
        }

        private static LanguageVersion? ToLanguageVersion(string langVersionPropertyValue)
        {
            if (!(string.IsNullOrWhiteSpace(langVersionPropertyValue) || langVersionPropertyValue.Equals("Default", StringComparison.OrdinalIgnoreCase)))
            {
                // ISO-1, ISO-2, 3, 4, 5, 6 or Default
                switch (langVersionPropertyValue.ToLower())
                {
                    case "iso-1": return LanguageVersion.CSharp1;
                    case "iso-2": return LanguageVersion.CSharp2;
                    case "3": return LanguageVersion.CSharp3;
                    case "4": return LanguageVersion.CSharp4;
                    case "5": return LanguageVersion.CSharp5;
                    case "6": return LanguageVersion.CSharp6;
                }
            }
            return null;
        }
    }
}
