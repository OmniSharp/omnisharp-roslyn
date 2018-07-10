using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Composition;
using System.Composition.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Options;
using OmniSharp.Roslyn;
using OmniSharp.Roslyn.Options;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp
{
    public class WorkspaceInitializer
    {
        public static void Initialize(
            IServiceProvider serviceProvider,
            CompositionHost compositionHost,
            IConfiguration configuration,
            ILogger logger)
        {
            var workspace = compositionHost.GetExport<OmniSharpWorkspace>();
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<OmniSharpOptions>>();

            var projectEventForwarder = compositionHost.GetExport<ProjectEventForwarder>();
            projectEventForwarder.Initialize();
            var projectSystems = compositionHost.GetExports<IProjectSystem>();
            var nodes = new List<Node<IProjectSystem>>();
            foreach(var projectSystem in projectSystems)
            {
                nodes.Add(Node<IProjectSystem>.From<DisplayNameAttribute>(projectSystem, attribute => attribute.DisplayName));
            }
            var graph = Graph<IProjectSystem>.GetGraph(nodes);
            var sortedList = graph.TopologicalSort();

            // Initialize all the project systems
            foreach (var projectSystem in sortedList)
            {
                try
                {
                    var projectConfiguration = configuration.GetSection(projectSystem.Key);
                    var enabledProjectFlag = projectConfiguration.GetValue("enabled", defaultValue: projectSystem.EnabledByDefault);
                    if (enabledProjectFlag)
                    {
                        projectSystem.Initalize(projectConfiguration);
                    }
                    else
                    {
                        logger.LogInformation($"Project system '{projectSystem.GetType().FullName}' is disabled in the configuration.");
                    }
                }
                catch (Exception e)
                {
                    var message = $"The project system '{projectSystem.GetType().FullName}' threw exception during initialization.";
                    // if a project system throws an unhandled exception it should not crash the entire server
                    logger.LogError(e, message);
                }
            }

            ProvideWorkspaceOptions(compositionHost, workspace, options, logger);

            // Mark the workspace as initialized
            workspace.Initialized = true;

            // when configuration options change
            // run workspace options providers automatically
            options.OnChange(o =>
            {
                ProvideWorkspaceOptions(compositionHost, workspace, options, logger);
            });

            logger.LogInformation("Configuration finished.");
        }

        private static void ProvideWorkspaceOptions(
            CompositionHost compositionHost,
            OmniSharpWorkspace workspace,
            IOptionsMonitor<OmniSharpOptions> options,
            ILogger logger)
        {
            // run all workspace options providers
            foreach (var workspaceOptionsProvider in compositionHost.GetExports<IWorkspaceOptionsProvider>())
            {
                var providerName = workspaceOptionsProvider.GetType().FullName;

                try
                {
                    LoggerExtensions.LogInformation(logger, $"Invoking Workspace Options Provider: {providerName}");
                    workspace.Options = workspaceOptionsProvider.Process(workspace.Options, options.CurrentValue.FormattingOptions);
                }
                catch (Exception e)
                {
                    var message = $"The workspace options provider '{providerName}' threw exception during initialization.";
                    logger.LogError(e, message);
                }
            }
        }
    }
}
