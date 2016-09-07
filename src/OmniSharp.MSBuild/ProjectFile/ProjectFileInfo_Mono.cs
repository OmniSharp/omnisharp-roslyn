#if NET451

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Build.BuildEngine;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Models;
using OmniSharp.Options;

namespace OmniSharp.MSBuild.ProjectFile
{
    public partial class ProjectFileInfo
    {
        private static ProjectFileInfo CreateForMono(
            string projectFilePath,
            string solutionDirectory,
            MSBuildOptions options,
            ILogger logger,
            ICollection<MSBuildDiagnosticsMessage> diagnostics)
        {
            // On mono we need to use this API since the ProjectCollection
            // isn't fully implemented
#pragma warning disable CS0618
            var engine = Engine.GlobalEngine;
            engine.DefaultToolsVersion = "4.0";
#pragma warning restore CS0618

            // engine.RegisterLogger(new ConsoleLogger());
            engine.RegisterLogger(new MSBuildLogForwarder(logger, diagnostics));

            var globalProperties = new BuildPropertyGroup();
            globalProperties.SetProperty(PropertyNames.DesignTimeBuild, value: true);
            globalProperties.SetProperty(PropertyNames.BuildProjectReferences, value: false);
            // Dump entire assembly reference closure
            globalProperties.SetProperty(PropertyNames._ResolveReferenceDependencies, value: true);
            globalProperties.SetProperty(PropertyNames.SolutionDir, solutionDirectory + Path.DirectorySeparatorChar);

            // propertyGroup.SetProperty("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");

            engine.GlobalProperties = globalProperties;

            var project = engine.CreateNewProject();
            project.Load(projectFilePath);

            var buildResult = engine.BuildProjectFile(
                projectFilePath,
                targetNames: new[] { TargetNames.ResolveReferences },
                globalProperties: globalProperties,
                targetOutputs: null,
                buildFlags: BuildSettings.None,
                toolsVersion: null);

            if (!buildResult)
            {
                return null;
            }

            var items = project.EvaluatedItems.OfType<BuildItem>()
                                              .ToLookup(g => g.Name);

            var properties = project.EvaluatedProperties.OfType<BuildProperty>()
                                                        .ToDictionary(p => p.Name);

            var assemblyName = properties.GetFinalValue(PropertyNames.AssemblyName);
            var name = Path.GetFileNameWithoutExtension(projectFilePath);

            var targetFrameworkMoniker = properties.GetFinalValue(PropertyNames.TargetFrameworkMoniker);
            var targetFramework = targetFrameworkMoniker != null
                ? new FrameworkName(targetFrameworkMoniker)
                : null;

            var langVersion = PropertyConverter.ToLanguageVersion(properties.GetFinalValue(PropertyNames.LangVersion));
            var projectGuid = PropertyConverter.ToGuid(properties.GetFinalValue(PropertyNames.ProjectGuid));
            var targetPath = properties.GetFinalValue(PropertyNames.TargetPath);
            var allowUnsafe = PropertyConverter.ToBoolean(properties.GetFinalValue(PropertyNames.AllowUnsafeBlocks));
            var signAssembly = PropertyConverter.ToBoolean(properties.GetFinalValue(PropertyNames.SignAssembly));
            var assemblyOriginatorKeyFile = properties.GetFinalValue(PropertyNames.AssemblyOriginatorKeyFile);
            var documentationFile = properties.GetFinalValue(PropertyNames.DocumentationFile);
            var defineConstants = PropertyConverter.ToDefineConstants(properties.GetFinalValue(PropertyNames.DefineConstants));

            var projectDirectory = Path.GetDirectoryName(projectFilePath);

            // REVIEW: FullPath metadata value returns the wrong physical path. We need to figure out why.
            // Are we setting up something incorrectly?
            var sourceFiles = items[ItemNames.Compile]
                .Select(item => GetFullPath(item, projectDirectory))
                .ToList();

            var references = items[ItemNames.ReferencePath]
                .Where(item => !item.HasMetadata(MetadataNames.Project))
                .Select(item => GetFullPath(item, projectDirectory))
                .ToList();

            var projectReferences = items[ItemNames.ProjectReference]
                .Select(item => GetFullPath(item, projectDirectory))
                .ToList();

            var analyzers = items[ItemNames.Analyzer]
                .Select(item => GetFullPath(item, projectDirectory))
                .ToList();

            return new ProjectFileInfo(
                projectFilePath,
                assemblyName,
                name,
                targetFramework,
                langVersion,
                projectGuid,
                targetPath,
                allowUnsafe ?? false,
                OutputKind.ConsoleApplication,
                signAssembly ?? false,
                assemblyOriginatorKeyFile,
                !string.IsNullOrWhiteSpace(documentationFile),
                defineConstants,
                sourceFiles,
                references,
                projectReferences,
                analyzers);
        }

        private static string GetFullPath(BuildItem item, string projectDirectory)
        {
            return Path.GetFullPath(Path.Combine(projectDirectory, item.FinalItemSpec));
        }
    }

    internal static class BuildEngineExtensions
    {
        public static void SetProperty(this BuildPropertyGroup buildPropertyGroup, string name, bool value)
        {
            buildPropertyGroup.SetProperty(name, value ? "true" : "false");
        }

        public static string GetFinalValue(this Dictionary<string, BuildProperty> dictionary, string key)
        {
            BuildProperty buildProperty;

            return dictionary.TryGetValue(key, out buildProperty)
                ? buildProperty.FinalValue
                : null;
        }
    }
}

#endif
