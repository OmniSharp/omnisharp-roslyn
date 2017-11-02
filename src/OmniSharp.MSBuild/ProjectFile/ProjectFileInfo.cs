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
using OmniSharp.MSBuild.Discovery;
using OmniSharp.MSBuild.Logging;
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

        public static (ProjectFileInfo projectFileInfo, ImmutableArray<MSBuildDiagnostic> diagnostics) Create(
            string filePath, string solutionDirectory, ILogger logger, MSBuildInstance msbuildInstance, MSBuildOptions options = null)
        {
            if (!File.Exists(filePath))
            {
                return (null, ImmutableArray<MSBuildDiagnostic>.Empty);
            }

            var (projectInstance, diagnostics) = LoadProject(filePath, solutionDirectory, logger, msbuildInstance, options);
            if (projectInstance == null)
            {
                return (null, diagnostics);
            }

            var id = ProjectId.CreateNewId(debugName: filePath);
            var data = ProjectData.Create(projectInstance);
            var projectFileInfo = new ProjectFileInfo(id, filePath, data);

            return (projectFileInfo, diagnostics);
        }

        private static (ProjectInstance projectInstance, ImmutableArray<MSBuildDiagnostic> diagnostics) LoadProject(
            string filePath, string solutionDirectory, ILogger logger,
            MSBuildInstance msbuildInstance, MSBuildOptions options)
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
            var targetFrameworks = PropertyConverter.SplitList(project.GetPropertyValue(PropertyNames.TargetFrameworks), ';');

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
            var msbuildLogger = new MSBuildLogger(logger);
            var buildResult = projectInstance.Build(
                targets: new string[] { TargetNames.Compile, TargetNames.CoreCompile },
                loggers: new[] { msbuildLogger });

            var diagnostics = msbuildLogger.GetDiagnostics();

            return buildResult
                ? (projectInstance, diagnostics)
                : (null, diagnostics);
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

        public (ProjectFileInfo projectFileInfo, ImmutableArray<MSBuildDiagnostic> diagnostics) Reload(
            string solutionDirectory, ILogger logger, MSBuildInstance msbuildInstance, MSBuildOptions options = null)
        {
            var (projectInstance, diagnostics) = LoadProject(FilePath, solutionDirectory, logger, msbuildInstance, options);
            if (projectInstance == null)
            {
                return (null, diagnostics);
            }

            var data = ProjectData.Create(projectInstance);
            var projectFileInfo = new ProjectFileInfo(Id, FilePath, data);

            return (projectFileInfo, diagnostics);
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
    }
}
