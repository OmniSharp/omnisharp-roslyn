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
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using OmniSharp.Endpoint;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.FileWatching;
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
using FileSystemWatcher = OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher;

namespace OmniSharp.LanguageServerProtocol
{
    public class LanguageServerHost : IDisposable
    {
        private readonly LanguageServerOptions _options;
        private IServiceCollection _services;
        private readonly CommandLineApplication _application;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private CompositionHost _compositionHost;
        private IServiceProvider _serviceProvider;

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
                .WithServices(ConfigureServices);

            _application = application;
            _cancellationTokenSource = cancellationTokenSource;
        }

        /// <summary>
        /// Used for inject the test host for unit testing
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="configureServer"></param>
        /// <param name="cancellationTokenSource"></param>
        internal LanguageServerHost(
            Stream input,
            Stream output,
            Action<LanguageServerOptions> configureServer,
            CancellationTokenSource cancellationTokenSource)
        {
            _options = new LanguageServerOptions()
                .WithInput(input)
                .WithOutput(output)
                .WithServices(ConfigureServices);

            configureServer(_options);

            _cancellationTokenSource = cancellationTokenSource;
        }

        private void ConfigureServices(IServiceCollection services)
        {
            _services = services;
            services.AddSingleton(new ConfigurationItem()
            {
                Section = "csharp"
            });
            services.AddSingleton(new ConfigurationItem()
            {
                Section = "omnisharp"
            });
        }

        public void Dispose()
        {
            _compositionHost?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        public async Task Start()
        {
            var server = Server = await LanguageServer.From(_options);
            server.Exit.Subscribe(Observer.Create<int>(i => _cancellationTokenSource.Cancel()));

            WorkspaceInitializer.Initialize(_serviceProvider, _compositionHost);

            var environment = _compositionHost.GetExport<IOmniSharpEnvironment>();
            var logger = _compositionHost.GetExport<ILoggerFactory>().CreateLogger<LanguageServerHost>();

            logger.LogInformation($"Omnisharp server running using Lsp at location '{environment.TargetDirectory}' on host {environment.HostProcessId}.");

            Console.CancelKeyPress += (sender, e) =>
            {
                _cancellationTokenSource.Cancel();
                e.Cancel = true;
            };

            if (environment.HostProcessId != -1)
            {
                try
                {
                    var hostProcess = Process.GetProcessById(environment.HostProcessId);
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

        internal LanguageServer Server { get; set; }

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

        private static (IServiceProvider serviceProvider, CompositionHost compositionHost) CreateCompositionHost(
            ILanguageServer server,
            InitializeParams initializeParams,
            CommandLineApplication application,
            IServiceCollection services
        )
        {
            var logLevel = GetLogLevel(initializeParams.Trace);
            var environment = new OmniSharpEnvironment(
                Helpers.FromUri(initializeParams.RootUri),
                Convert.ToInt32(initializeParams.ProcessId ?? -1L),
                application.LogLevel < logLevel ? application.LogLevel : logLevel,
                application.OtherArgs.ToArray());

            var configurationRoot = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddConfiguration(new ConfigurationBuilder(environment).Build())
                    .AddConfiguration(server.Configuration.GetSection("csharp"))
                    .AddConfiguration(server.Configuration.GetSection("omnisharp"))
                    .Build()
                ;

            var eventEmitter = new LanguageServerEventEmitter(server);

            services.AddSingleton(server);
            var serviceProvider =
                CompositionHostBuilder.CreateDefaultServiceProvider(environment, configurationRoot, eventEmitter,
                    services);

            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<LanguageServerHost>();

            var options = serviceProvider.GetRequiredService<IOptionsMonitor<OmniSharpOptions>>();
            var plugins = application.CreatePluginAssemblies(options.CurrentValue, environment);

            var assemblyLoader = serviceProvider.GetRequiredService<IAssemblyLoader>();
            var compositionHostBuilder = new CompositionHostBuilder(serviceProvider)
                .WithOmniSharpAssemblies()
                .WithAssemblies(typeof(LanguageServerHost).Assembly)
                .WithAssemblies(assemblyLoader.LoadByAssemblyNameOrPath(logger, plugins.AssemblyNames).ToArray());

            return (serviceProvider, compositionHostBuilder.Build());
        }

        internal static RequestHandlers ConfigureCompositionHost(ILanguageServer server,
            CompositionHost compositionHost)
        {
            var projectSystems = compositionHost.GetExports<IProjectSystem>();

            var documentSelectors = projectSystems
                .GroupBy(x => x.Language)
                .Select(x => (
                    language: x.Key,
                    selector: new DocumentSelector(x
                        .SelectMany(z => z.Extensions)
                        .Distinct()
                        .SelectMany(z =>
                        {
                            if (x.Key == LanguageNames.CSharp && z == ".cs")
                            {
                                return new[]
                                {
                                    new DocumentFilter() {Pattern = $"**/*{z}"},
                                    new DocumentFilter() {Scheme = "csharp"}
                                };
                            }

                            return new[]
                            {
                                new DocumentFilter() {Pattern = $"**/*{z}"},
                            };
                        })
                    )
                ))
                .ToArray();

            var logger = compositionHost.GetExport<ILoggerFactory>().CreateLogger<LanguageServerHost>();

            logger.LogTrace(
                "Configured Document Selectors {@DocumentSelectors}",
                documentSelectors.Select(x => new {x.language, x.selector})
            );

            var omnisharpRequestHandlers =
                compositionHost.GetExports<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>>();
            // TODO: Get these with metadata so we can attach languages
            // This will then let us build up a better document filter, and add handles foreach type of handler
            // This will mean that we will have a strategy to create handlers from the interface type
            var handlers = new RequestHandlers(omnisharpRequestHandlers, documentSelectors);

            logger.LogTrace("--- Handler Definitions ---");
            foreach (var handlerCollection in handlers)
            {
                foreach (var handler in handlerCollection)
                {
                    logger.LogTrace(
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
                var interop = InitializeInterop(compositionHost);
                foreach (var osHandler in interop)
                {
                    var method = $"o#/{osHandler.Key.Trim('/').ToLowerInvariant()}";
                    r.OnJsonRequest(method, CreateInteropHandler(osHandler.Value));
                    logger.LogTrace("O# Handler: {Method}", method);
                }

                static Func<JToken, CancellationToken, Task<JToken>> CreateInteropHandler(
                    Lazy<LanguageProtocolInteropHandler> handler) => async (request, cancellationToken) =>
                {
                    var response = await handler.Value.Handle(request);
                    return response == null ? JValue.CreateNull() : JToken.FromObject(response);
                };

                r.OnRequest<JToken, object>($"o#/{OmniSharpEndpoints.CheckAliveStatus.Trim('/').ToLowerInvariant()}",
                    (request, cancellationToken) => Task.FromResult<object>(true));
                r.OnRequest<JToken, object>($"o#/{OmniSharpEndpoints.CheckReadyStatus.Trim('/').ToLowerInvariant()}",
                    (request, cancellationToken) =>
                        Task.FromResult<object>(compositionHost.GetExport<OmniSharpWorkspace>().Initialized));
                r.OnRequest<JToken, object>($"o#/{OmniSharpEndpoints.StopServer.Trim('/').ToLowerInvariant()}",
                    async (request, cancellationToken) => await server.Shutdown.ToTask(cancellationToken));
            });
            logger.LogTrace("--- Handler Definitions ---");

            return handlers;
        }

        private Task Initialize(ILanguageServer server, InitializeParams initializeParams,
            CancellationToken cancellationToken)
        {
            (_serviceProvider, _compositionHost) =
                CreateCompositionHost(server, initializeParams, _application, _services);
            var handlers = ConfigureCompositionHost(server, _compositionHost);
            RegisterHandlers(server, _compositionHost, handlers);

            server.Register(s =>
            {
                s.AddHandler(
                    new OmnisharpOnDidChangeWatchedFilesHandler(
                        _serviceProvider.GetRequiredService<IFileSystemNotifier>()));
                s.AddHandler(
                    new OmnisharpOnDidChangeWatchedDirectoriesHandler(_serviceProvider
                        .GetRequiredService<IFileSystemNotifier>()));
            });

            return Task.CompletedTask;
        }

        internal void UnderTest(IServiceProvider serviceProvider, CompositionHost compositionHost)
        {
            _serviceProvider = serviceProvider;
            _compositionHost = compositionHost;
        }

        internal static void RegisterHandlers(ILanguageServer server, CompositionHost compositionHost,
            RequestHandlers handlers)
        {
            // TODO: Make it easier to resolve handlers from MEF (without having to add more attributes to the services if we can help it)
            var workspace = compositionHost.GetExport<OmniSharpWorkspace>();
            compositionHost.GetExport<DiagnosticEventForwarder>().IsEnabled = true;
            server.Register(s =>
            {
                foreach (var handler in OmniSharpTextDocumentSyncHandler.Enumerate(handlers, workspace)
                    .Concat(OmniSharpDefinitionHandler.Enumerate(handlers))
                    .Concat(OmniSharpHoverHandler.Enumerate(handlers))
                    .Concat(OmniSharpCompletionHandler.Enumerate(handlers))
                    .Concat(OmniSharpSignatureHelpHandler.Enumerate(handlers))
                    .Concat(OmniSharpRenameHandler.Enumerate(handlers))
                    .Concat(OmniSharpWorkspaceSymbolsHandler.Enumerate(handlers))
                    .Concat(OmniSharpDocumentSymbolHandler.Enumerate(handlers))
                    .Concat(OmniSharpReferencesHandler.Enumerate(handlers))
                    .Concat(OmniSharpCodeLensHandler.Enumerate(handlers))
                    .Concat(OmniSharpCodeActionHandler.Enumerate(handlers))
                    .Concat(OmniSharpDocumentFormattingHandler.Enumerate(handlers))
                    .Concat(OmniSharpDocumentFormatRangeHandler.Enumerate(handlers))
                    .Concat(OmniSharpExecuteCommandHandler.Enumerate(handlers))
                    .Concat(OmniSharpDocumentOnTypeFormattingHandler.Enumerate(handlers)))
                {
                    s.AddHandlers(handler);
                }
            });
        }

        private static IDictionary<string, Lazy<LanguageProtocolInteropHandler>> InitializeInterop(
            CompositionHost compositionHost)
        {
            var workspace = compositionHost.GetExport<OmniSharpWorkspace>();
            var projectSystems = compositionHost.GetExports<IProjectSystem>();
            var endpointMetadatas = compositionHost.GetExports<Lazy<IRequest, OmniSharpEndpointMetadata>>()
                .Select(x => x.Metadata)
                .ToArray();

            var handlers = compositionHost.GetExports<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>>();

            IDictionary<string, Lazy<LanguageProtocolInteropHandler>> endpointHandlers = null;
            var updateBufferEndpointHandler = new Lazy<LanguageProtocolInteropHandler<UpdateBufferRequest, object>>(
                () => (LanguageProtocolInteropHandler<UpdateBufferRequest, object>) endpointHandlers[
                    OmniSharpEndpoints.UpdateBuffer].Value);
            var languagePredicateHandler = new LanguagePredicateHandler(projectSystems);
            var projectSystemPredicateHandler = new StaticLanguagePredicateHandler("Projects");
            var nugetPredicateHandler = new StaticLanguagePredicateHandler("NuGet");
            endpointHandlers = endpointMetadatas.ToDictionary(
                x => x.EndpointName,
                endpoint => new Lazy<LanguageProtocolInteropHandler>(() =>
                {
                    IPredicateHandler handler;

                    // Projects are a special case, this allows us to select the correct "Projects" language for them
                    if (endpoint.EndpointName == OmniSharpEndpoints.ProjectInformation ||
                        endpoint.EndpointName == OmniSharpEndpoints.WorkspaceInformation)
                        handler = projectSystemPredicateHandler;
                    else if (endpoint.EndpointName == OmniSharpEndpoints.PackageSearch ||
                             endpoint.EndpointName == OmniSharpEndpoints.PackageSource ||
                             endpoint.EndpointName == OmniSharpEndpoints.PackageVersion)
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
                        updateEndpointHandler =
                            new Lazy<LanguageProtocolInteropHandler<UpdateBufferRequest, object>>(() => null);
                    }

                    return LanguageProtocolInteropHandler.Factory(handler, endpoint, handlers, updateEndpointHandler);
                }),
                StringComparer.OrdinalIgnoreCase
            );

            return endpointHandlers;
        }
    }
}
