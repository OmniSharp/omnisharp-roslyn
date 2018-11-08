using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.Logging;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Options;

using MSB = Microsoft.Build;

namespace OmniSharp.MSBuild
{
    internal class ProjectLoader
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, string> _globalProperties;
        private readonly MSBuildOptions _options;
        private readonly SdksPathResolver _sdksPathResolver;

        public ProjectLoader(MSBuildOptions options, string solutionDirectory, ImmutableDictionary<string, string> propertyOverrides, ILoggerFactory loggerFactory, SdksPathResolver sdksPathResolver)
        {
            _logger = loggerFactory.CreateLogger<ProjectLoader>();
            _options = options ?? new MSBuildOptions();
            _sdksPathResolver = sdksPathResolver ?? throw new ArgumentNullException(nameof(sdksPathResolver));
            _globalProperties = CreateGlobalProperties(_options, solutionDirectory, propertyOverrides, _logger);
        }

        private static Dictionary<string, string> CreateGlobalProperties(
            MSBuildOptions options, string solutionDirectory, ImmutableDictionary<string, string> propertyOverrides, ILogger logger)
        {
            var globalProperties = new Dictionary<string, string>
            {
                { PropertyNames.DesignTimeBuild, "true" },
                { PropertyNames.BuildingInsideVisualStudio, "true" },
                { PropertyNames.BuildProjectReferences, "false" },
                { PropertyNames._ResolveReferenceDependencies, "true" },
                { PropertyNames.SolutionDir, solutionDirectory + Path.DirectorySeparatorChar },

                // Setting this property will cause any XAML markup compiler tasks to run in the
                // current AppDomain, rather than creating a new one. This is important because
                // our AppDomain.AssemblyResolve handler for MSBuild will not be connected to
                // the XAML markup compiler's AppDomain, causing the task not to be able to find
                // MSBuild.
                { PropertyNames.AlwaysCompileMarkupFilesInSeparateDomain, "false" },

                // This properties allow the design-time build to handle the Compile target without actually invoking the compiler.
                // See https://github.com/dotnet/roslyn/pull/4604 for details.
                { PropertyNames.ProvideCommandLineArgs, "true" },
                { PropertyNames.SkipCompilerExecution, "true" }
            };

            globalProperties.AddPropertyOverride(PropertyNames.MSBuildExtensionsPath, options.MSBuildExtensionsPath, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.TargetFrameworkRootPath, options.TargetFrameworkRootPath, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.RoslynTargetsPath, options.RoslynTargetsPath, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.CscToolPath, options.CscToolPath, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.CscToolExe, options.CscToolExe, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.VisualStudioVersion, options.VisualStudioVersion, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.Configuration, options.Configuration, propertyOverrides, logger);
            globalProperties.AddPropertyOverride(PropertyNames.Platform, options.Platform, propertyOverrides, logger);

            if (propertyOverrides.TryGetValue(PropertyNames.BypassFrameworkInstallChecks, out var value))
            {
                globalProperties.Add(PropertyNames.BypassFrameworkInstallChecks, value);
            }

            return globalProperties;
        }

        public (MSB.Execution.ProjectInstance projectInstance, ImmutableArray<MSBuildDiagnostic> diagnostics) BuildProject(string filePath)
        {
            using (_sdksPathResolver.SetSdksPathEnvironmentVariable(filePath))
            {
                var evaluatedProject = EvaluateProjectFileCore(filePath);

                SetTargetFrameworkIfNeeded(evaluatedProject);

                var projectInstance = evaluatedProject.CreateProjectInstance();
                var msbuildLogger = new MSBuildLogger(_logger);

                var loggers = new List<MSB.Framework.ILogger>()
                {
                    msbuildLogger
                };

                if (_options.GenerateBinaryLogs)
                {
                    var binlogPath = Path.ChangeExtension(projectInstance.FullPath, ".binlog");
                    var binaryLogger = new MSB.Logging.BinaryLogger()
                    {
                        CollectProjectImports = MSB.Logging.BinaryLogger.ProjectImportsCollectionMode.Embed,
                        Parameters = binlogPath
                    };

                    loggers.Add(binaryLogger);
                }

                var buildResult = projectInstance.Build(
                    targets: new string[] { TargetNames.Compile, TargetNames.CoreCompile },
                    loggers);

                var diagnostics = msbuildLogger.GetDiagnostics();

                return buildResult
                    ? (projectInstance, diagnostics)
                    : (null, diagnostics);
            }
        }

        public MSB.Evaluation.Project EvaluateProjectFile(string filePath)
        {
            using (_sdksPathResolver.SetSdksPathEnvironmentVariable(filePath))
            {
                return EvaluateProjectFileCore(filePath);
            }
        }

        private MSB.Evaluation.Project EvaluateProjectFileCore(string filePath)
        {
            // Evaluate the MSBuild project
            var projectCollection = new MSB.Evaluation.ProjectCollection(_globalProperties);

            var toolsVersion = _options.ToolsVersion;
            if (string.IsNullOrEmpty(toolsVersion) || Version.TryParse(toolsVersion, out _))
            {
                toolsVersion = projectCollection.DefaultToolsVersion;
            }

            toolsVersion = GetLegalToolsetVersion(toolsVersion, projectCollection.Toolsets);

            var project = projectCollection.LoadProject(filePath, toolsVersion);

            SetTargetFrameworkIfNeeded(project);

            return project;
        }

        private static void SetTargetFrameworkIfNeeded(MSB.Evaluation.Project evaluatedProject)
        {
            var targetFramework = evaluatedProject.GetPropertyValue(PropertyNames.TargetFramework);
            var targetFrameworks = PropertyConverter.SplitList(evaluatedProject.GetPropertyValue(PropertyNames.TargetFrameworks), ';');

            // If the project supports multiple target frameworks and specific framework isn't
            // selected, we must pick one before execution. Otherwise, the ResolveReferences
            // target might not be available to us.
            if (string.IsNullOrWhiteSpace(targetFramework) && targetFrameworks.Length > 0)
            {
                // For now, we'll just pick the first target framework. Eventually, we'll need to
                // do better and potentially allow OmniSharp hosts to select a target framework.
                targetFramework = targetFrameworks[0];
                evaluatedProject.SetProperty(PropertyNames.TargetFramework, targetFramework);
                evaluatedProject.ReevaluateIfNecessary();
            }
        }

        private string GetLegalToolsetVersion(string toolsVersion, ICollection<MSB.Evaluation.Toolset> toolsets)
        {
            // Does the expected tools version exist? If so, use it.
            foreach (var toolset in toolsets)
            {
                if (toolset.ToolsVersion == toolsVersion)
                {
                    return toolsVersion;
                }
            }

            // If not, try to find the highest version available and use that instead.

            Version highestVersion = null;

            var legalToolsets = new SortedList<Version, MSB.Evaluation.Toolset>(toolsets.Count);
            foreach (var toolset in toolsets)
            {
                // Only consider this toolset if it has a legal version, we haven't seen it, and its path exists.
                if (Version.TryParse(toolset.ToolsVersion, out var toolsetVersion) &&
                    !legalToolsets.ContainsKey(toolsetVersion) &&
                    Directory.Exists(toolset.ToolsPath))
                {
                    legalToolsets.Add(toolsetVersion, toolset);

                    if (highestVersion == null ||
                        toolsetVersion > highestVersion)
                    {
                        highestVersion = toolsetVersion;
                    }
                }
            }

            if (legalToolsets.Count == 0 || highestVersion == null)
            {
                _logger.LogError($"No legal MSBuild tools available, defaulting to {toolsVersion}.");
                return toolsVersion;
            }

            var result = legalToolsets[highestVersion].ToolsVersion;

            _logger.LogInformation($"Using MSBuild tools version: {result}");

            return result;
        }
    }
}
