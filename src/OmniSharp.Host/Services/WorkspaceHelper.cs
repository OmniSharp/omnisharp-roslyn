using System;
using System.Composition.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.Options;
using OmniSharp.Roslyn;
using OmniSharp.Roslyn.Options;

namespace OmniSharp.Services
{
    public class WorkspaceHelper
    {
        private readonly CompositionHost _compositionHost;
        private readonly IConfiguration _configuration;
        private readonly OmniSharpOptions _options;
        private readonly ILogger _logger;

        public WorkspaceHelper(CompositionHost compositionHost, IConfiguration configuration, OmniSharpOptions options, ILoggerFactory loggerFactory)
        {
            _compositionHost = compositionHost;
            _configuration = configuration;
            _options = options;
            _logger = loggerFactory.CreateLogger<WorkspaceHelper>();
        }

        public void Initialize(OmniSharpWorkspace workspace)
        {
            var projectEventForwarder = _compositionHost.GetExport<ProjectEventForwarder>();
            projectEventForwarder.Initialize();

            // Initialize all the project systems discovered with MEF
            foreach (var projectSystem in _compositionHost.GetExports<IProjectSystem>())
            {
                try
                {
                    var projectConfiguration = _configuration.GetSection(projectSystem.Key);
                    var enabledProjectFlag = projectConfiguration.GetValue<bool>("enabled", defaultValue: true);
                    if (enabledProjectFlag)
                    {
                        projectSystem.Initalize(projectConfiguration);
                    }
                    else
                    {
                        _logger.LogInformation($"Project system '{projectSystem.GetType().FullName}' is disabled in the configuration.");
                    }
                }
                catch (Exception e)
                {
                    var message = $"The project system '{projectSystem.GetType().FullName}' threw exception during initialization.";
                    // if a project system throws an unhandled exception it should not crash the entire server
                    _logger.LogError(e, message);
                }
            }

            ProvideOptions(workspace, _options);

            // Mark the workspace as initialized
            workspace.Initialized = true;
        }

        public void ProvideOptions(OmniSharpWorkspace workspace, OmniSharpOptions options)
        {
            // run all workspace options providers discovered with MEF
            foreach (var workspaceOptionsProvider in _compositionHost.GetExports<IWorkspaceOptionsProvider>())
            {
                var providerName = workspaceOptionsProvider.GetType().FullName;

                try
                {
                    _logger.LogInformation($"Invoking Workspace Options Provider: {providerName}");
                    workspace.Options = workspaceOptionsProvider.Process(workspace.Options, options.FormattingOptions);
                }
                catch (Exception e)
                {
                    var message = $"The workspace options provider '{providerName}' threw exception during initialization.";
                    _logger.LogError(e, message);
                }
            }
        }
    }
}
