using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;

#if ASPNET50
using Microsoft.Build.BuildEngine;
#endif

namespace OmniSharp.MSBuild.ProjectFile
{
    public class ProjectFileInfo
    {
        public ProjectId WorkspaceId { get; set; }

        public Guid ProjectId { get; private set; }

        public string Name { get; private set; }

        public string ProjectFilePath { get; private set; }

        public FrameworkName TargetFramework { get; private set; }

        public string ProjectDirectory
        {
            get
            {
                return Path.GetDirectoryName(ProjectFilePath);
            }
        }

        public string AssemblyName { get; private set; }

        public string TargetPath { get; private set; }

        public IEnumerable<string> SourceFiles { get; private set; }

        public IEnumerable<string> References { get; private set; }

        public IEnumerable<string> ProjectReferences { get; private set; }

        public static ProjectFileInfo Create(string solutionDirectory, string projectFilePath)
        {
            var projectFileInfo = new ProjectFileInfo();
            projectFileInfo.ProjectFilePath = projectFilePath;

#if ASPNET50
#pragma warning disable CS0618
            var engine = Engine.GlobalEngine;
#pragma warning restore CS0618
            // engine.RegisterLogger(new ConsoleLogger());

            var propertyGroup = new BuildPropertyGroup();
            propertyGroup.SetProperty("Configuration", "Debug");
            propertyGroup.SetProperty("DesignTimeBuild", "true");
            propertyGroup.SetProperty("BuildProjectReferences", "false");
            // Dump entire assembly reference closure
            propertyGroup.SetProperty("_ResolveReferenceDependencies", "true");
            propertyGroup.SetProperty("SolutionDir", solutionDirectory + Path.DirectorySeparatorChar);

            // propertyGroup.SetProperty("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");

            engine.GlobalProperties = propertyGroup;

            var project = engine.CreateNewProject();
            project.Load(projectFilePath);
            var buildResult = engine.BuildProjectFile(projectFilePath, new[] { "ResolveReferences" }, propertyGroup, null, BuildSettings.None, null);

            if (!buildResult)
            {
                return null;
            }

            var itemsLookup = project.EvaluatedItems.OfType<BuildItem>()
                                                    .ToLookup(g => g.Name);

            var properties = project.EvaluatedProperties.OfType<BuildProperty>()
                                                        .ToDictionary(p => p.Name);

            projectFileInfo.AssemblyName = properties["AssemblyName"].FinalValue;
            projectFileInfo.Name = properties["ProjectName"].FinalValue;
            projectFileInfo.TargetFramework = new FrameworkName(properties["TargetFrameworkMoniker"].FinalValue);
            projectFileInfo.ProjectId = new Guid(properties["ProjectGuid"].FinalValue.TrimStart('{').TrimEnd('}'));
            projectFileInfo.TargetPath = properties["TargetPath"].FinalValue;

            projectFileInfo.SourceFiles = itemsLookup["Compile"]
                .Select(b => Path.GetFullPath(Path.Combine(projectFileInfo.ProjectDirectory, b.FinalItemSpec)))
                .ToList();

            // TODO: Remove project references
            projectFileInfo.References = itemsLookup["ReferencePath"]
                .Select(p => Path.GetFullPath(Path.Combine(projectFileInfo.ProjectDirectory, p.FinalItemSpec)))
                .ToList();

            projectFileInfo.ProjectReferences = itemsLookup["ProjectReference"]
                .Select(p => Path.GetFullPath(Path.Combine(projectFileInfo.ProjectDirectory, p.FinalItemSpec)))
                .ToList();
#else
            // TODO: Shell out to msbuild/xbuild here?
#endif
            return projectFileInfo;
        }
    }
}