using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using OmniSharp.Endpoint;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.LanguageServerProtocol.Eventing;
using OmniSharp.LanguageServerProtocol.Handlers;
using OmniSharp.Mef;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Options;
using OmniSharp.Plugins;
using OmniSharp.Protocol;
using OmniSharp.Roslyn;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.LanguageServerProtocol
{
    internal class LanguageServerHost : IDisposable
    {
        private readonly LanguageServerOptions _options;
        private IServiceCollection _services;
        private readonly CommandLineApplication _application;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private CompositionHost _compositionHost;
        private LanguageServerEventEmitter _eventEmitter;
        private IServiceProvider _serviceProvider;
        private RequestHandlers _handlers;
        private OmniSharpEnvironment _environment;
        private ILogger<LanguageServerHost> _logger;

        public LanguageServerHost(
            Stream input,
            Stream output,
            CommandLineApplication application,
            CancellationTokenSource cancellationTokenSource)
        {
            _options = new LanguageServerOptions()
                .WithInput(input)
                .WithOutput(output)
                .ConfigureLogging(x => x
                    .AddLanguageProtocolLogging()
                    // .SetMinimumLevel(application.LogLevel)
                )
                .OnInitialize(Initialize)
                .WithServices(services =>
                {
                    _services = services;
                });

            _application = application;
            _cancellationTokenSource = cancellationTokenSource;
        }

        public void Dispose()
        {
            _compositionHost?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        public async Task Start()
        {
            var server = await LanguageServer.From(_options);
            server.Exit.Subscribe(Observer.Create<int>(i => _cancellationTokenSource.Cancel()));

            WorkspaceInitializer.Initialize(_serviceProvider, _compositionHost);

            _logger.LogInformation($"Omnisharp server running using Lsp at location '{_environment.TargetDirectory}' on host {_environment.HostProcessId}.");

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

        private static LogLevel GetLogLevel(InitializeTrace initializeTrace)
        {
            switch (initializeTrace)
            {
                case InitializeTrace.Verbose:
                    return LogLevel.Trace;
                case InitializeTrace.Messages:
                    return LogLevel.Debug;
                case InitializeTrace.Off:
                    return LogLevel.Information;

                default:
                    return LogLevel.Information;
            }
        }

        private void CreateCompositionHost(ILanguageServer server, InitializeParams initializeParams)
        {
            var logLevel = GetLogLevel(initializeParams.Trace);
            _environment = new OmniSharpEnvironment(
                Helpers.FromUri(initializeParams.RootUri),
                Convert.ToInt32(initializeParams.ProcessId ?? -1L),
                _application.LogLevel < logLevel ? _application.LogLevel : logLevel,
                _application.OtherArgs.ToArray());

            var configurationRoot = new ConfigurationBuilder(_environment).Build();
            _eventEmitter = new LanguageServerEventEmitter(server);

            _services.AddSingleton(server);
            _serviceProvider = CompositionHostBuilder.CreateDefaultServiceProvider(_environment, configurationRoot, _eventEmitter, _services);

            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
            _logger = loggerFactory.CreateLogger<LanguageServerHost>();

            var options = _serviceProvider.GetRequiredService<IOptionsMonitor<OmniSharpOptions>>();
            var plugins = _application.CreatePluginAssemblies(options.CurrentValue, _environment);

            var assemblyLoader = _serviceProvider.GetRequiredService<IAssemblyLoader>();
            var compositionHostBuilder = new CompositionHostBuilder(_serviceProvider)
                .WithOmniSharpAssemblies()
                .WithAssemblies(typeof(LanguageServerHost).Assembly)
                .WithAssemblies(assemblyLoader.LoadByAssemblyNameOrPath(_logger, plugins.AssemblyNames).ToArray());

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

            _logger.LogTrace(
                "Configured Document Selectors {@DocumentSelectors}",
                documentSelectors.Select(x => new { x.language, x.selector })
            );

            var omnisharpRequestHandlers = _compositionHost.GetExports<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>>();
            // TODO: Get these with metadata so we can attach languages
            // This will thne let us build up a better document filter, and add handles foreach type of handler
            // This will mean that we will have a strategy to create handlers from the interface type
            _handlers = new RequestHandlers(omnisharpRequestHandlers, documentSelectors);

            _logger.LogTrace("--- Handler Definitions ---");
            foreach (var handlerCollection in _handlers)
            {
                foreach (var handler in handlerCollection)
                {
                    _logger.LogTrace(
                        "Handler: {Language}:{DocumentSelector}:{Handler}",
                        handlerCollection.Language,
                        handlerCollection.DocumentSelector.ToString(),
                        handler.GetType().FullName
                    );
                }
            }

            // the goal here is add interoperability between omnisharp and LSP
            // This way an existing client (say vscode) that is using the custom omnisharp protocol can migrate to the new one
            // and not loose any functionality.
            server.Register(r =>
            {
                var interop = InitializeInterop();
                foreach (var osHandler in interop)
                {
                    var method = $"o#/{osHandler.Key.Trim('/')}";
                    r.OnJsonRequest(method, CreateInteropHandler(osHandler.Value));
                    _logger.LogTrace("O# Handler: {Method}", method);
                }

                static Func<JToken, CancellationToken, Task<JToken>> CreateInteropHandler(Lazy<LanguageProtocolInteropHandler> handler) => async (request, cancellationToken) =>
                {
                    var response = await handler.Value.Handle(request);
                    return response == null ? JValue.CreateNull() : JToken.FromObject(response);
                };

                r.OnRequest<JToken, object>($"o#/{OmniSharpEndpoints.CheckAliveStatus.Trim('/')}",
                    (request, cancellationToken) => Task.FromResult<object>(true));
                r.OnRequest<JToken, object>($"o#/{OmniSharpEndpoints.CheckReadyStatus.Trim('/')}",
                    (request, cancellationToken) => Task.FromResult<object>(_compositionHost.GetExport<OmniSharpWorkspace>().Initialized));
                r.OnRequest<JToken, object>($"o#/{OmniSharpEndpoints.StopServer.Trim('/')}",
                    async (request, cancellationToken) => await server.Shutdown.ToTask(cancellationToken));
            });
            _logger.LogTrace("--- Handler Definitions ---");
        }

        private Task Initialize(ILanguageServer server, InitializeParams initializeParams, CancellationToken cancellationToken)
        {
            CreateCompositionHost(server, initializeParams);

            // TODO: Make it easier to resolve handlers from MEF (without having to add more attributes to the services if we can help it)
            var workspace = _compositionHost.GetExport<OmniSharpWorkspace>();
            _compositionHost.GetExport<DiagnosticEventForwarder>().IsEnabled = true;
            server.Register(s =>
            {
                foreach (var handler in OmniSharpTextDocumentSyncHandler.Enumerate(_handlers, workspace)
                    .Concat(OmniSharpDefinitionHandler.Enumerate(_handlers))
                    .Concat(OmniSharpHoverHandler.Enumerate(_handlers))
                    .Concat(OmniSharpCompletionHandler.Enumerate(_handlers))
                    .Concat(OmniSharpSignatureHelpHandler.Enumerate(_handlers))
                    .Concat(OmniSharpRenameHandler.Enumerate(_handlers))
                    .Concat(OmniSharpWorkspaceSymbolsHandler.Enumerate(_handlers))
                .Concat(OmniSharpDocumentSymbolHandler.Enumerate(_handlers))
                    .Concat(OmniSharpReferencesHandler.Enumerate(_handlers))
                    .Concat(OmniSharpCodeLensHandler.Enumerate(_handlers))
                    .Concat(OmniSharpCodeActionHandler.Enumerate(_handlers))
                    .Concat(OmniSharpDocumentFormattingHandler.Enumerate(_handlers))
                    .Concat(OmniSharpDocumentFormatRangeHandler.Enumerate(_handlers))
                    .Concat(OmniSharpExecuteCommandHandler.Enumerate(_handlers))
                    .Concat(OmniSharpDocumentOnTypeFormattingHandler.Enumerate(_handlers)))
                {
                    s.AddHandlers(handler);
                }
            });

            return Task.CompletedTask;
        }

        private IDictionary<string, Lazy<LanguageProtocolInteropHandler>> InitializeInterop()
        {
            var workspace = _compositionHost.GetExport<OmniSharpWorkspace>();
            var projectSystems = _compositionHost.GetExports<IProjectSystem>();
            var endpointMetadatas = _compositionHost.GetExports<Lazy<IRequest, OmniSharpEndpointMetadata>>()
                .Select(x => x.Metadata)
                .ToArray();

            var handlers = _compositionHost.GetExports<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>>();

            IDictionary<string, Lazy<LanguageProtocolInteropHandler>> endpointHandlers = null;
            var updateBufferEndpointHandler = new Lazy<LanguageProtocolInteropHandler<UpdateBufferRequest, object>>(
                () => (LanguageProtocolInteropHandler<UpdateBufferRequest, object>)endpointHandlers[OmniSharpEndpoints.UpdateBuffer].Value);
            var languagePredicateHandler = new LanguagePredicateHandler(projectSystems);
            var projectSystemPredicateHandler = new StaticLanguagePredicateHandler("Projects");
            var nugetPredicateHandler = new StaticLanguagePredicateHandler("NuGet");
            endpointHandlers = endpointMetadatas.ToDictionary(
                x => x.EndpointName,
                endpoint => new Lazy<LanguageProtocolInteropHandler>(() =>
                {
                    IPredicateHandler handler;

                    // Projects are a special case, this allows us to select the correct "Projects" language for them
                    if (endpoint.EndpointName == OmniSharpEndpoints.ProjectInformation || endpoint.EndpointName == OmniSharpEndpoints.WorkspaceInformation)
                        handler = projectSystemPredicateHandler;
                    else if (endpoint.EndpointName == OmniSharpEndpoints.PackageSearch || endpoint.EndpointName == OmniSharpEndpoints.PackageSource || endpoint.EndpointName == OmniSharpEndpoints.PackageVersion)
                        handler = nugetPredicateHandler;
                    else
                        handler = languagePredicateHandler;

                    // This lets any endpoint, that contains a Request object, invoke update buffer.
                    // The language will be same language as the caller, this means any language service
                    // must implement update buffer.
                    var updateEndpointHandler = updateBufferEndpointHandler;
                    if (endpoint.EndpointName == OmniSharpEndpoints.UpdateBuffer)
                    {
                        // We don't want to call update buffer on update buffer.
                        updateEndpointHandler = new Lazy<LanguageProtocolInteropHandler<UpdateBufferRequest, object>>(() => null);
                    }

                    return LanguageProtocolInteropHandler.Factory(handler, endpoint, handlers, updateEndpointHandler);
                }),
                StringComparer.OrdinalIgnoreCase
            );

            return endpointHandlers;
        }
    }
}
