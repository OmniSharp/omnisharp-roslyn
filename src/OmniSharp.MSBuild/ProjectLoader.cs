using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.BuildHost;
using OmniSharp.MSBuild.Logging;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Options;

namespace OmniSharp.MSBuild
{
    internal class ProjectLoader : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly Dictionary<string, string> _globalProperties;
        private readonly string _dotNetPath;
        private readonly Dictionary<string, RoslynBuildHost> _buildHosts = new();
        private readonly object _buildHostsGate = new();
        private bool _disposed;

        public ProjectLoader(MSBuildOptions options, string solutionDirectory, ImmutableDictionary<string, string> propertyOverrides, ILoggerFactory loggerFactory, string dotNetPath)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<ProjectLoader>();
            _dotNetPath = dotNetPath ?? throw new ArgumentNullException(nameof(dotNetPath));
            _globalProperties = CreateGlobalProperties(options ?? new MSBuildOptions(), solutionDirectory, propertyOverrides, _logger);
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

                { PropertyNames.AlwaysCompileMarkupFilesInSeparateDomain, "false" },

                // This properties allow the design-time build to handle the Compile target without actually invoking the compiler.
                // See https://github.com/dotnet/roslyn/pull/4604 for details.
                { PropertyNames.ProvideCommandLineArgs, "true" },
                { PropertyNames.SkipCompilerExecution, "true" },

                // Ensures the SDK doesn't try to generate app hosts for the loading projects
                { PropertyNames.UseAppHost, "false" },
            };

            foreach (var propertyOverride in propertyOverrides)
            {
                globalProperties[propertyOverride.Key] = propertyOverride.Value;
                logger.LogDebug($"'{propertyOverride.Key}' set to '{propertyOverride.Value}'");
            }

            SetOptionOverride(PropertyNames.Configuration, options.Configuration);
            SetOptionOverride(PropertyNames.Platform, options.Platform);

            return globalProperties;

            void SetOptionOverride(string name, string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    globalProperties[name] = value;
                    logger.LogDebug($"'{name}' set to '{value}' (user override)");
                }
            }
        }

        public (BuildHostProject project, ImmutableArray<MSBuildDiagnostic> diagnostics) BuildProject(
            string filePath,
            IReadOnlyDictionary<string, string> configurationsInSolution,
            bool forceReload = false)
        {
            var properties = GetProjectProperties(filePath, configurationsInSolution);
            var key = string.Join("\n", properties.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}={pair.Value}"));
            RoslynBuildHost buildHost;
            lock (_buildHostsGate)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
                if (forceReload && _buildHosts.TryGetValue(key, out var existingBuildHost))
                {
                    _buildHosts.Remove(key);
                    existingBuildHost.Dispose();
                }
                if (!_buildHosts.TryGetValue(key, out buildHost))
                {
                    buildHost = new RoslynBuildHost(properties, _dotNetPath, _loggerFactory);
                    _buildHosts.Add(key, buildHost);
                }
            }

            var result = buildHost.LoadProject(filePath);
            var diagnostics = result.Diagnostics.Select(diagnostic => MSBuildDiagnostic.Create(
                diagnostic.IsError ? MSBuildDiagnosticSeverity.Error : MSBuildDiagnosticSeverity.Warning,
                diagnostic.Message,
                diagnostic.ProjectFilePath)).ToImmutableArray();
            foreach (var diagnostic in result.Diagnostics)
            {
                if (diagnostic.IsError)
                {
                    _logger.LogError(diagnostic.Message);
                }
                else
                {
                    _logger.LogWarning(diagnostic.Message);
                }
            }

            return (result.Project, diagnostics);
        }

        private Dictionary<string, string> GetProjectProperties(
            string filePath,
            IReadOnlyDictionary<string, string> projectConfigurationsInSolution)
        {
            var localProperties = new Dictionary<string, string>(_globalProperties);
            if (projectConfigurationsInSolution != null &&
                localProperties.TryGetValue(PropertyNames.Configuration, out var solutionConfiguration))
            {
                if (!localProperties.TryGetValue(PropertyNames.Platform, out var solutionPlatform))
                {
                    solutionPlatform = "Any CPU";
                }

                var solutionSelector = $"{solutionConfiguration}|{solutionPlatform}.ActiveCfg";
                _logger.LogDebug($"Found configuration `{solutionSelector}` in solution for '{filePath}'.");

                if (projectConfigurationsInSolution.TryGetValue(solutionSelector, out var projectSelector))
                {
                    var parts = projectSelector.Split('|');
                    if (parts.Length == 2)
                    {
                        localProperties[PropertyNames.Configuration] = parts[0];
                        localProperties[PropertyNames.Platform] = parts[1].Replace("Any CPU", "AnyCPU");
                        _logger.LogDebug($"Using configuration from solution: `{parts[0]}|{localProperties[PropertyNames.Platform]}`");
                    }
                }
            }

            return localProperties;
        }

        public void Dispose()
        {
            lock (_buildHostsGate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                foreach (var buildHost in _buildHosts.Values)
                {
                    buildHost.Dispose();
                }

                _buildHosts.Clear();
            }
        }
    }
}
