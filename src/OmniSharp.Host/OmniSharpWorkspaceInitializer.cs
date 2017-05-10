using System;
using System.Composition.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Options;
using OmniSharp.Roslyn;
using OmniSharp.Roslyn.Options;
using OmniSharp.Services;

namespace OmniSharp
{
    public class OmniSharpWorkspaceInitializer
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly CompositionHost _compositionHost;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IOptionsMonitor<OmniSharpOptions> _options;

        public OmniSharpWorkspaceInitializer(
            IServiceProvider serviceProvider,
            CompositionHost compositionHost,
            IConfiguration configuration,
            ILogger logger
        )
        {
            _workspace = compositionHost.GetExport<OmniSharpWorkspace>();
            _compositionHost = compositionHost;
            _configuration = configuration;
            _logger = logger;
            _options = serviceProvider.GetRequiredService<IOptionsMonitor<OmniSharpOptions>>();
        }

        public void Initialize()
        {
            var projectEventForwarder = _compositionHost.GetExport<ProjectEventForwarder>();
            projectEventForwarder.Initialize();

            // Initialize all the project systems
            foreach (var projectSystem in _compositionHost.GetExports<IProjectSystem>())
            {
                try
                {
                    projectSystem.Initalize(_configuration.GetSection(projectSystem.Key));
                }
                catch (Exception e)
                {
                    var message = $"The project system '{projectSystem.GetType().FullName}' threw exception during initialization.";
                    // if a project system throws an unhandled exception it should not crash the entire server
                    LoggerExceptions.LogError(_logger, e, message);
                }
            }

            ProvideWorkspaceOptions();

            // Mark the workspace as initialized
            _workspace.Initialized = true;

            // when configuration options change
            // run workspace options providers automatically
            _options.OnChange(o =>
            {
                ProvideWorkspaceOptions();
            });

            LoggerExtensions.LogInformation(_logger, "Configuration finished.");
        }

        private void ProvideWorkspaceOptions()
        {
            // run all workspace options providers
            foreach (var workspaceOptionsProvider in _compositionHost.GetExports<IWorkspaceOptionsProvider>())
            {
                var providerName = workspaceOptionsProvider.GetType().FullName;

                try
                {
                    LoggerExtensions.LogInformation(_logger, $"Invoking Workspace Options Provider: {providerName}");
                    _workspace.Options = workspaceOptionsProvider.Process(_workspace.Options, _options.CurrentValue.FormattingOptions);
                }
                catch (Exception e)
                {
                    var message = $"The workspace options provider '{providerName}' threw exception during initialization.";
                    LoggerExceptions.LogError(_logger, e, message);
                }
            }
        }
    }
}