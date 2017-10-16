using System;
using System.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Endpoint;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Plugins;
using OmniSharp.Services;
using OmniSharp.Utilities;
using OmniSharp.Stdio.Services;
using OmniSharp.Models.ChangeBuffer;
using OmniSharp.Mef;
using OmniSharp.Models.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.LanguageServerProtocol.Eventing;
using OmniSharp.Extensions.LanguageServer.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.LanguageServerProtocol.Handlers;

namespace OmniSharp.LanguageServerProtocol
{
    class LanguageServerHost : IDisposable
    {
        private readonly ServiceCollection _services;
        private readonly LanguageServer _server;
        private CompositionHost _compositionHost;
        private readonly LanguageServerLoggerFactory _loggerFactory;
        private readonly CommandLineApplication _application;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private IConfiguration _configuration;
        private IServiceProvider _serviceProvider;
        private RequestHandlers _handlers;
        private OmniSharpEnvironment _environment;

        public LanguageServerHost(
            Stream input,
            Stream output,
            CommandLineApplication application,
            CancellationTokenSource cancellationTokenSource)
        {
            _services = new ServiceCollection();
            _loggerFactory = new LanguageServerLoggerFactory();
            _services.AddSingleton<ILoggerFactory>(_loggerFactory);
            _server = new LanguageServer(input, output, _loggerFactory);
            _server.OnInitialize(Initialize);
            _application = application;
            _cancellationTokenSource = cancellationTokenSource;
        }

        public void Dispose()
        {
            _compositionHost?.Dispose();
            _loggerFactory?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        private static LogLevel GetLogLevel(InitializeTrace initializeTrace)
        {
            switch (initializeTrace)
            {
                case InitializeTrace.verbose:
                    return LogLevel.Trace;
                case InitializeTrace.off:
                    return LogLevel.Warning;
                case InitializeTrace.messages:
                default:
                    return LogLevel.Information;
            }
        }

        private void CreateCompositionHost(InitializeParams initializeParams)
        {
            _environment = new OmniSharpEnvironment(
                Helpers.FromUri(initializeParams.RootUri),
                Convert.ToInt32(initializeParams.ProcessId ?? -1L),
                GetLogLevel(initializeParams.Trace),
                _application.OtherArgs.ToArray());

            _loggerFactory.AddProvider(_server, _environment);

            _configuration = new ConfigurationBuilder(_environment).Build();
            _serviceProvider = CompositionHostBuilder.CreateDefaultServiceProvider(_configuration, _services);
            // TODO: Make this work with logger factory differently
            // Maybe create a child logger factory?
            // _loggerFactory
            //     .AddLanguageServer(_server, (category, level) => HostHelpers.LogFilter(category, level, _environment));

            var eventEmitter = new LanguageServerEventEmitter(_server);
            var plugins = _application.CreatePluginAssemblies();
            var compositionHostBuilder = new CompositionHostBuilder(_serviceProvider, _environment, eventEmitter)
                .WithOmniSharpAssemblies()
                .WithAssemblies(typeof(LanguageServerHost).Assembly)
                .WithAssemblies(plugins.AssemblyNames.Select(Assembly.Load).ToArray());

            _compositionHost = compositionHostBuilder.Build();

            var projectSystems = _compositionHost.GetExports<IProjectSystem>();

            var documentSelectors = projectSystems
                .GroupBy(x => x.Language)
                .Select(x => (
                    language: x.Key,
                    selector: new DocumentSelector(x
                        .SelectMany(z => z.Extensions)
                        .Distinct()
                        .Select(z => new DocumentFilter()
                        {
                            Pattern = $"**/*{z}"
                        }))
                    ));

            // TODO: Get these with metadata so we can attach languages
            // This will thne let us build up a better document filter, and add handles foreach type of handler
            // This will mean that we will have a strategy to create handlers from the interface type
            _handlers = new RequestHandlers(
                _compositionHost.GetExports<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>>(),
                documentSelectors
            );
        }

        private Task Initialize(InitializeParams initializeParams)
        {
            CreateCompositionHost(initializeParams);

            // TODO: Make it easier to resolve handlers from MEF (without having to add more attributes to the services if we can help it)
            var workspace = _compositionHost.GetExport<OmniSharpWorkspace>();

            _server.AddHandlers(TextDocumentSyncHandler.Enumerate(_handlers, workspace));
            _server.AddHandlers(DefinitionHandler.Enumerate(_handlers));
            _server.AddHandlers(HoverHandler.Enumerate(_handlers));
            _server.AddHandlers(CompletionHandler.Enumerate(_handlers));

            _server.LogMessage(new LogMessageParams()
            {
                Message = "Added handlers... waiting for initialize...",
                Type = MessageType.Log
            });

            return Task.CompletedTask;
        }

        public async Task Start()
        {
            _server.LogMessage(new LogMessageParams()
            {
                Message = "Starting server...",
                Type = MessageType.Log
            });

            await _server.Initialize();

            _server.LogMessage(new LogMessageParams()
            {
                Message = "initialized...",
                Type = MessageType.Log
            });

            var logger = _loggerFactory.CreateLogger(typeof(LanguageServerHost));
            WorkspaceInitializer.Initialize(_serviceProvider, _compositionHost, _configuration, logger);

            // Kick on diagnostics
            var diagnosticHandler = _handlers.GetAll()
                .OfType<Mef.IRequestHandler<DiagnosticsRequest, DiagnosticsResponse>>();

            foreach (var handler in diagnosticHandler)
                await handler.Handle(new DiagnosticsRequest());

            logger.LogInformation($"Omnisharp server running using Lsp at location '{_environment.TargetDirectory}' on host {_environment.HostProcessId}.");

            Console.CancelKeyPress += (sender, e) =>
            {
                _cancellationTokenSource.Cancel();
                e.Cancel = true;
            };

            if (_environment.HostProcessId != -1)
            {
                try
                {
                    var hostProcess = Process.GetProcessById(_environment.HostProcessId);
                    hostProcess.EnableRaisingEvents = true;
                    hostProcess.OnExit(() => _cancellationTokenSource.Cancel());
                }
                catch
                {
                    // If the process dies before we get here then request shutdown
                    // immediately
                    _cancellationTokenSource.Cancel();
                }
            }
        }
    }
}
