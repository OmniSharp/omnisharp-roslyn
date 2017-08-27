using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Endpoint;
using OmniSharp.Mef;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Plugins;
using OmniSharp.Services;
using OmniSharp.Stdio.Logging;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Stdio
{
    public class Host : IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly TextReader _input;
        private readonly ISharedTextWriter _writer;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDictionary<string, Lazy<EndpointHandler>> _endpointHandlers;
        private readonly CompositionHost _compositionHost;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOmniSharpEnvironment _environment;
        private readonly CancellationTokenSource _cancellation;

        public Host(
            TextReader input, ISharedTextWriter writer, IOmniSharpEnvironment environment, IConfiguration configuration,
            IServiceProvider serviceProvider, CompositionHost compositionHost, ILoggerFactory loggerFactory, CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
            _input = input;
            _writer = writer;
            _environment = environment;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _compositionHost = compositionHost;
            _loggerFactory = loggerFactory;

            var handlers = Initialize();
            _endpointHandlers = handlers;
        }

        private IDictionary<string, Lazy<EndpointHandler>> Initialize()
        {
            var workspace = _compositionHost.GetExport<OmniSharpWorkspace>();
            var projectSystems = _compositionHost.GetExports<IProjectSystem>();
            var logger = _loggerFactory.CreateLogger<Program>();
            var endpointMetadatas = _compositionHost.GetExports<Lazy<IRequest, OmniSharpEndpointMetadata>>()
                .Select(x => x.Metadata)
                .ToArray();

            var handlers = _compositionHost.GetExports<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>>();

            var updateBufferEndpointHandler = new Lazy<EndpointHandler<UpdateBufferRequest, object>>(
                () => (EndpointHandler<UpdateBufferRequest, object>)_endpointHandlers[OmniSharpEndpoints.UpdateBuffer].Value);
            var languagePredicateHandler = new LanguagePredicateHandler(projectSystems);
            var projectSystemPredicateHandler = new StaticLanguagePredicateHandler("Projects");
            var nugetPredicateHandler = new StaticLanguagePredicateHandler("NuGet");
            var endpointHandlers = endpointMetadatas.ToDictionary(
                x => x.EndpointName,
                endpoint => new Lazy<EndpointHandler>(() =>
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
                        updateEndpointHandler = new Lazy<EndpointHandler<UpdateBufferRequest, object>>(() => null);
                    }

                    return EndpointHandler.Factory(handler, _compositionHost, logger, endpoint, handlers, updateEndpointHandler, Enumerable.Empty<Plugin>());
                }),
                StringComparer.OrdinalIgnoreCase
            );


            // Handled as alternative middleware in http
            endpointHandlers.Add(
                OmniSharpEndpoints.CheckAliveStatus,
                new Lazy<EndpointHandler>(
                    () => new GenericEndpointHandler(x => Task.FromResult<object>(true)))
            );
            endpointHandlers.Add(
                OmniSharpEndpoints.CheckReadyStatus,
                new Lazy<EndpointHandler>(
                    () => new GenericEndpointHandler(x => Task.FromResult<object>(workspace.Initialized)))
            );
            endpointHandlers.Add(
                OmniSharpEndpoints.StopServer,
                new Lazy<EndpointHandler>(
                    () => new GenericEndpointHandler(x =>
                    {
                        _cancellation.Cancel();
                        return Task.FromResult<object>(null);
                    }))
            );

            return endpointHandlers;
        }

        public void Dispose()
        {
            _compositionHost?.Dispose();
            _loggerFactory?.Dispose();
            _cancellation?.Dispose();
        }

        public void Start()
        {
            var logger = _loggerFactory.CreateLogger<Program>();
            _loggerFactory.AddStdio(_writer, (category, level) => OmniSharp.LogFilter(category, level, _environment));

            new OmniSharpWorkspaceInitializer(_serviceProvider, _compositionHost, _configuration, logger).Initialize();

            Task.Factory.StartNew(async () =>
            {
                _writer.WriteLine(new EventPacket()
                {
                    Event = "started"
                });

                while (!_cancellation.IsCancellationRequested)
                {
                    var line = await _input.ReadLineAsync();
                    if (line == null)
                    {
                        break;
                    }

                    var ignored = Task.Factory.StartNew(async () =>
                    {
                        try
                        {
                            await HandleRequest(line);
                        }
                        catch (Exception e)
                        {
                            _writer.WriteLine(new EventPacket()
                            {
                                Event = "error",
                                Body = JsonConvert.ToString(e.ToString(), '"', StringEscapeHandling.Default)
                            });
                        }
                    });
                }
            });

            logger.LogInformation($"Omnisharp server running using {nameof(TransportType.Stdio)} at location '{_environment.TargetDirectory}' on host {_environment.HostProcessId}.");

            Console.CancelKeyPress += (sender, e) =>
            {
                _cancellation.Cancel();
                e.Cancel = true;
            };

            if (_environment.HostProcessId != -1)
            {
                try
                {
                    var hostProcess = Process.GetProcessById(_environment.HostProcessId);
                    hostProcess.EnableRaisingEvents = true;
                    hostProcess.OnExit(() => _cancellation.Cancel());
                }
                catch
                {
                    // If the process dies before we get here then request shutdown
                    // immediately
                    _cancellation.Cancel();
                }
            }
        }

        private async Task HandleRequest(string json)
        {
            var request = RequestPacket.Parse(json);
            var response = request.Reply();

            try
            {
                // hand off request to next layer
                if (_endpointHandlers.TryGetValue(request.Command, out var handler))
                {
                    var result = await handler.Value.Handle(request);
                    response.Body = result;
                    return;
                }
                throw new NotSupportedException($"Command '{request.Command}' is not supported.");
            }
            catch (Exception e)
            {
                // updating the response object here so that the ResponseStream
                // prints the latest state when being closed
                response.Success = false;
                response.Message = JsonConvert.ToString(e.ToString(), '"', StringEscapeHandling.Default);
            }
            finally
            {
                // actually write it
                _writer.WriteLine(response);
            }
        }
    }
}
