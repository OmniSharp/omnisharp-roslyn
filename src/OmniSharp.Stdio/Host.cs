using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Endpoint;
using OmniSharp.Mef;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Plugins;
using OmniSharp.Services;
using OmniSharp.Protocol;
using OmniSharp.Utilities;
using System.Globalization;

namespace OmniSharp.Stdio
{
    internal class Host : IDisposable
    {
        private readonly TextReader _input;
        private readonly ISharedTextWriter _writer;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDictionary<string, Lazy<EndpointHandler>> _endpointHandlers;
        private readonly CompositionHost _compositionHost;
        private readonly ILogger _logger;
        private readonly IOmniSharpEnvironment _environment;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CachedStringBuilder _cachedStringBuilder;
        private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        public Host(
            TextReader input, ISharedTextWriter writer, IOmniSharpEnvironment environment,
            IServiceProvider serviceProvider, CompositionHostBuilder compositionHostBuilder, ILoggerFactory loggerFactory, CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _input = input;
            _writer = writer;
            _environment = environment;
            _serviceProvider = serviceProvider;
            _logger = loggerFactory.CreateLogger<Host>();

            _logger.LogInformation($"Starting OmniSharp on {Platform.Current}");

            _compositionHost = compositionHostBuilder.Build(_environment.TargetDirectory);
            _cachedStringBuilder = new CachedStringBuilder();

            var handlers = Initialize();
            _endpointHandlers = handlers;
        }

        private IDictionary<string, Lazy<EndpointHandler>> Initialize()
        {
            var workspace = _compositionHost.GetExport<OmniSharpWorkspace>();
            var projectSystems = _compositionHost.GetExports<IProjectSystem>();
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

                    return EndpointHandler.Factory(handler, _compositionHost, _logger, endpoint, handlers, updateEndpointHandler, Enumerable.Empty<Plugin>());
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
                        _cancellationTokenSource.Cancel();
                        return Task.FromResult<object>(null);
                    }))
            );

            return endpointHandlers;
        }

        public void Dispose()
        {
            _compositionHost?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        public void Start()
        {
            WorkspaceInitializer.Initialize(_serviceProvider, _compositionHost);

            Task.Factory.StartNew(async () =>
            {
                _writer.WriteLine(new EventPacket()
                {
                    Event = "started"
                });

                while (!_cancellationTokenSource.IsCancellationRequested)
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
                            await HandleRequest(line, _logger);
                        }
                        catch (Exception e)
                        {
                            if (e is AggregateException aggregateEx)
                            {
                                e = aggregateEx.Flatten().InnerException;
                            }

                            _writer.WriteLine(new EventPacket()
                            {
                                Event = "error",
                                Body = JsonConvert.ToString(e.ToString(), '"', StringEscapeHandling.Default)
                            });
                        }
                    });
                }
            });

            _logger.LogInformation($"Omnisharp server running using {nameof(TransportType.Stdio)} at location '{_environment.TargetDirectory}' on host {_environment.HostProcessId}.");

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

        private async Task HandleRequest(string json, ILogger logger)
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            var request = RequestPacket.Parse(json);
            if (logger.IsEnabled(LogLevel.Debug))
            {
                LogRequest(json, logger, LogLevel.Debug);
            }

            var response = request.Reply();

            try
            {
                if (!request.Command.StartsWith("/"))
                {
                    request.Command = $"/{request.Command}";
                }
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
                if (e is AggregateException aggregateEx)
                {
                    e = aggregateEx.Flatten().InnerException;
                }

                // updating the response object here so that the ResponseStream
                // prints the latest state when being closed
                response.Success = false;
                response.Message = JsonConvert.ToString(e.ToString(), '"', StringEscapeHandling.Default);
            }
            finally
            {
                // response gets logged when Debug or more detailed log level is enabled
                // or when we have unsuccessful response (exception)
                if (logger.IsEnabled(LogLevel.Debug) || !response.Success)
                {
                    // if logging is at Debug level, request would have already been logged
                    // however not for higher log levels, so we want to explicitly log the request too
                    if (!logger.IsEnabled(LogLevel.Debug))
                    {
                        LogRequest(json, logger, LogLevel.Warning);
                    }

                    var currentTimestamp = Stopwatch.GetTimestamp();
                    var elapsed = new TimeSpan((long)(TimestampToTicks * (currentTimestamp - startTimestamp)));

                    LogResponse(response.ToString(), logger, response.Success, elapsed);
                }

                // actually write it
                _writer.WriteLine(response);
            }
        }

        void LogRequest(string json, ILogger logger, LogLevel logLevel)
        {
            var builder = _cachedStringBuilder.Acquire();
            try
            {
                builder.AppendLine("************ Request ************");
                builder.Append(JToken.Parse(json).ToString(Formatting.Indented));
                logger.Log(logLevel, builder.ToString());
            }
            finally
            {
                _cachedStringBuilder.Release(builder);
            }
        }

        void LogResponse(string json, ILogger logger, bool isSuccess, TimeSpan elapsed)
        {
            var builder = _cachedStringBuilder.Acquire();
            try
            {
                builder.AppendLine($"************  Response ({elapsed.TotalMilliseconds.ToString("0.0000", CultureInfo.InvariantCulture)}ms) ************ ");
                builder.Append(JToken.Parse(json).ToString(Formatting.Indented));

                if (isSuccess)
                {
                    logger.LogDebug(builder.ToString());
                }
                else
                {
                    logger.LogError(builder.ToString());
                }
            }
            finally
            {
                _cachedStringBuilder.Release(builder);
            }
        }
    }
}
