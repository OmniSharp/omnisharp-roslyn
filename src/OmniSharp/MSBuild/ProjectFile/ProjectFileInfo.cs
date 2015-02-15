using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.Framework.Logging;

#if ASPNET50
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Evaluation;
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

        public IList<string> SourceFiles { get; private set; }

        public IList<string> References { get; private set; }

        public IList<string> ProjectReferences { get; private set; }

        public IList<string> Analyzers { get; private set; }

        public static ProjectFileInfo Create(ILogger logger, string solutionDirectory, string projectFilePath)
        {
            var projectFileInfo = new ProjectFileInfo();
            projectFileInfo.ProjectFilePath = projectFilePath;

#if ASPNET50
            if (!PlatformHelper.IsMono)
            {
                var properties = new Dictionary<string, string>
                {
                    { "DesignTimeBuild", "true" },
                    { "BuildProjectReferences", "false" },
                    { "_ResolveReferenceDependencies", "true" },
                    { "SolutionDir", solutionDirectory + Path.DirectorySeparatorChar }
                };

                var collection = new ProjectCollection(properties);
                var project = collection.LoadProject(projectFilePath);
                var projectInstance = project.CreateProjectInstance();
                var buildResult = projectInstance.Build("ResolveReferences", loggers: null);

                if (!buildResult)
                {
                    return null;
                }

                projectFileInfo.AssemblyName = projectInstance.GetPropertyValue("AssemblyName");
                projectFileInfo.Name = projectInstance.GetPropertyValue("ProjectName");
                projectFileInfo.TargetFramework = new FrameworkName(projectInstance.GetPropertyValue("TargetFrameworkMoniker"));
                projectFileInfo.ProjectId = new Guid(projectInstance.GetPropertyValue("ProjectGuid").TrimStart('{').TrimEnd('}'));
                projectFileInfo.TargetPath = projectInstance.GetPropertyValue("TargetPath");

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
            }
            else
            {
                // On mono we need to use this API since the ProjectCollection
                // isn't fully implemented
#pragma warning disable CS0618
                var engine = Engine.GlobalEngine;
                engine.DefaultToolsVersion = "4.0";
#pragma warning restore CS0618
                // engine.RegisterLogger(new ConsoleLogger());

                var propertyGroup = new BuildPropertyGroup();
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
                projectFileInfo.Name = Path.GetFileNameWithoutExtension(projectFilePath);
                projectFileInfo.TargetFramework = new FrameworkName(properties["TargetFrameworkMoniker"].FinalValue);
                projectFileInfo.ProjectId = new Guid(properties["ProjectGuid"].FinalValue.TrimStart('{').TrimEnd('}'));
                projectFileInfo.TargetPath = properties["TargetPath"].FinalValue;

                // REVIEW: FullPath here returns the wrong physical path, we need to figure out
                // why. We must be setting up something incorrectly
                projectFileInfo.SourceFiles = itemsLookup["Compile"]
                    .Select(b => Path.GetFullPath(Path.Combine(projectFileInfo.ProjectDirectory, b.FinalItemSpec)))
                    .ToList();

                projectFileInfo.References = itemsLookup["ReferencePath"]
                    .Where(p => !p.HasMetadata("Project"))
                    .Select(p => Path.GetFullPath(Path.Combine(projectFileInfo.ProjectDirectory, p.FinalItemSpec)))
                    .ToList();

                projectFileInfo.ProjectReferences = itemsLookup["ProjectReference"]
                    .Select(p => Path.GetFullPath(Path.Combine(projectFileInfo.ProjectDirectory, p.FinalItemSpec)))
                    .ToList();

                projectFileInfo.Analyzers = itemsLookup["Analyzer"]
                    .Select(p => Path.GetFullPath(Path.Combine(projectFileInfo.ProjectDirectory, p.FinalItemSpec)))
                    .ToList();
            }
#else
            // TODO: Shell out to msbuild/xbuild here?
#endif
            return projectFileInfo;
        }
    }
}