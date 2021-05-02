using System;
using System.Composition.Hosting;
using System.Linq;
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
        public static void Initialize(IServiceProvider serviceProvider, CompositionHost compositionHost)
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<WorkspaceInitializer>();

            var workspace = compositionHost.GetExport<OmniSharpWorkspace>();
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<OmniSharpOptions>>();
            var configuration = serviceProvider.GetRequiredService<IConfigurationRoot>();
            var omnisharpEnvironment = serviceProvider.GetRequiredService<IOmniSharpEnvironment>();

            var projectEventForwarder = compositionHost.GetExport<ProjectEventForwarder>();
            projectEventForwarder.Initialize();

            workspace.EditorConfigEnabled = options.CurrentValue.FormattingOptions.EnableEditorConfigSupport;
            options.OnChange(x => workspace.EditorConfigEnabled = x.FormattingOptions.EnableEditorConfigSupport);

            logger.LogDebug("Starting with OmniSharp options: {options}", options.CurrentValue);
            ProvideWorkspaceOptions(compositionHost, workspace, options, logger, omnisharpEnvironment);

            // when configuration options change
            // run workspace options providers automatically
            options.OnChange(o =>
            {
                logger.LogDebug("OmniSharp options changed: {options}", options.CurrentValue);
                ProvideWorkspaceOptions(compositionHost, workspace, options, logger, omnisharpEnvironment);
            });

            var projectSystems = compositionHost.GetExports<IProjectSystem>();
            foreach (var projectSystem in projectSystems)
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

            // Mark the workspace as initialized
            workspace.Initialized = true;

            logger.LogInformation("Configuration finished.");
        }

        private static void ProvideWorkspaceOptions(
            CompositionHost compositionHost,
            OmniSharpWorkspace workspace,
            IOptionsMonitor<OmniSharpOptions> options,
            ILogger logger,
            IOmniSharpEnvironment omnisharpEnvironment)
        {
            // run all workspace options providers
            var workspaceOptionsProviders = compositionHost.GetExports<IWorkspaceOptionsProvider>().OrderBy(x => x.Order);
            foreach (var workspaceOptionsProvider in workspaceOptionsProviders)
            {
                var providerName = workspaceOptionsProvider.GetType().FullName;

                try
                {
                    logger.LogInformation($"Invoking Workspace Options Provider: {providerName}, Order: {workspaceOptionsProvider.Order}");
                    if (!workspace.TryApplyChanges(workspace.CurrentSolution.WithOptions(workspaceOptionsProvider.Process(workspace.Options, options.CurrentValue, omnisharpEnvironment))))
                    {
                        logger.LogWarning($"Couldn't apply options from Workspace Options Provider: {providerName}");
                    }
                }
                catch (Exception e)
                {
                    var message = $"The workspace options provider '{providerName}' threw exception during execution.";
                    logger.LogError(e, message);
                }
            }
        }
    }
}
