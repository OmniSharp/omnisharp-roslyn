using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Endpoint.Exports;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Plugins;
using OmniSharp.Protocol;

namespace OmniSharp.Endpoint
{
    public abstract class EndpointHandler
    {
        public abstract Task<object> Handle(RequestPacket packet);

        public static EndpointHandler Create<TRequest, TResponse>(IPredicateHandler languagePredicateHandler, CompositionHost host,
            ILogger logger,
            OmniSharpEndpointMetadata metadata,
            IEnumerable<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> handlers,
            Lazy<EndpointHandler<UpdateBufferRequest, object>> updateBufferHandler,
            IEnumerable<Plugin> plugins)
        {
            return new EndpointHandler<TRequest, TResponse>(languagePredicateHandler, host, logger, metadata, handlers.Where(x => x.Metadata.EndpointName == metadata.EndpointName), updateBufferHandler, plugins);
        }

        public static EndpointHandler Factory(IPredicateHandler languagePredicateHandler, CompositionHost host,
            ILogger logger,
            OmniSharpEndpointMetadata metadata,
            IEnumerable<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> handlers,
            Lazy<EndpointHandler<UpdateBufferRequest, object>> updateBufferHandler,
            IEnumerable<Plugin> plugins)
        {
            var createMethod = typeof(EndpointHandler).GetTypeInfo().DeclaredMethods.First(x => x.Name == nameof(EndpointHandler.Create));
            return (EndpointHandler)createMethod.MakeGenericMethod(metadata.RequestType, metadata.ResponseType).Invoke(null, new object[] { languagePredicateHandler, host, logger, metadata, handlers, updateBufferHandler, plugins });
        }
    }

    public class EndpointHandler<TRequest, TResponse> : EndpointHandler
    {
        private readonly CompositionHost _host;
        private readonly IPredicateHandler _languagePredicateHandler;
        private readonly Lazy<Task<Dictionary<string, ExportHandler<TRequest, TResponse>[]>>> _exports;
        private readonly OmniSharpWorkspace _workspace;
        private readonly bool _hasLanguageProperty;
        private readonly bool _hasFileNameProperty;
        private readonly bool _canBeAggregated;
        private readonly ILogger _logger;
        private readonly IEnumerable<Plugin> _plugins;
        private readonly Lazy<EndpointHandler<UpdateBufferRequest, object>> _updateBufferHandler;

        public EndpointHandler(IPredicateHandler languagePredicateHandler, CompositionHost host, ILogger logger, OmniSharpEndpointMetadata metadata, IEnumerable<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> handlers, Lazy<EndpointHandler<UpdateBufferRequest, object>> updateBufferHandler, IEnumerable<Plugin> plugins)
        {
            EndpointName = metadata.EndpointName;
            _host = host;
            _logger = logger;
            _languagePredicateHandler = languagePredicateHandler;
            _plugins = plugins;
            _workspace = host.GetExport<OmniSharpWorkspace>();

            _hasLanguageProperty = metadata.RequestType.GetRuntimeProperty(nameof(LanguageModel.Language)) != null;
            _hasFileNameProperty = metadata.RequestType.GetRuntimeProperty(nameof(Request.FileName)) != null;
            _canBeAggregated = typeof(IAggregateResponse).IsAssignableFrom(metadata.ResponseType);
            _updateBufferHandler = updateBufferHandler;

            _exports = new Lazy<Task<Dictionary<string, ExportHandler<TRequest, TResponse>[]>>>(() => LoadExportHandlers(handlers));
        }

        private Task<Dictionary<string, ExportHandler<TRequest, TResponse>[]>> LoadExportHandlers(IEnumerable<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> handlers)
        {
            var interfaceHandlers = handlers
                .Select(export => new RequestHandlerExportHandler<TRequest, TResponse>(export.Metadata.Language, (IRequestHandler<TRequest, TResponse>)export.Value))
                .Cast<ExportHandler<TRequest, TResponse>>();

            var plugins = _plugins.Where(x => x.Config.Endpoints.Contains(EndpointName))
                .Select(plugin => new PluginExportHandler<TRequest, TResponse>(EndpointName, plugin))
                .Cast<ExportHandler<TRequest, TResponse>>();

            // Group handlers by language and sort each group for consistency
            return Task.FromResult(interfaceHandlers
                .Concat(plugins)
                .GroupBy(export => export.Language, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(g => g).ToArray()));
        }

        public string EndpointName { get; }

        public override Task<object> Handle(RequestPacket packet)
        {
            var requestObject = DeserializeRequestObject(packet.ArgumentsStream);
            var model = GetLanguageModel(requestObject);

            return Process(packet, model, requestObject);
        }

        public async Task<object> Process(RequestPacket packet, LanguageModel model, JToken requestObject)
        {
            var request = requestObject.ToObject<TRequest>();
            if (request is Request && _updateBufferHandler.Value != null)
            {
                var realRequest = request as Request;
                if (!string.IsNullOrWhiteSpace(realRequest.FileName) && (realRequest.Buffer != null || realRequest.Changes != null))
                {
                    await _updateBufferHandler.Value.Process(packet, model, requestObject);
                }
            }

            if (_hasLanguageProperty)
            {
                // Handle cases where a request isn't aggrgate and a language isn't specified.
                // This helps with editors calling a legacy end point, for example /metadata
                if (!_canBeAggregated && string.IsNullOrWhiteSpace(model.Language))
                {
                    model.Language = LanguageNames.CSharp;
                }
                return await HandleLanguageRequest(model.Language, request, packet);
            }
            else if (_hasFileNameProperty)
            {
                var language = _languagePredicateHandler.GetLanguageForFilePath(model.FileName ?? string.Empty);
                return await HandleLanguageRequest(language, request, packet);
            }
            else
            {
                var language = _languagePredicateHandler.GetLanguageForFilePath(string.Empty);
                if (!string.IsNullOrEmpty(language))
                {
                    return await HandleLanguageRequest(language, request, packet);
                }
            }

            return await HandleAllRequest(request, packet);
        }

        private Task<object> HandleLanguageRequest(string language, TRequest request, RequestPacket packet)
        {
            if (!string.IsNullOrEmpty(language))
            {
                return HandleRequestForLanguage(language, request, packet);
            }

            return HandleAllRequest(request, packet);
        }

        private async Task<IAggregateResponse> AggregateResponsesFromLanguageHandlers(ExportHandler<TRequest, TResponse>[] handlers, TRequest request)
        {
            if (!_canBeAggregated)
            {
                throw new NotSupportedException($"Must be able to aggregate responses from all handlers for {EndpointName}");
            }

            IAggregateResponse aggregateResponse = null;

            if (handlers.Length == 1)
            {
                var response = handlers[0].Handle(request);
                return (IAggregateResponse)await response;
            }
            else
            {
                var responses = new List<Task<TResponse>>();
                foreach (var handler in handlers)
                {
                    responses.Add(handler.Handle(request));
                }

                foreach (IAggregateResponse response in await Task.WhenAll(responses))
                {
                    if (aggregateResponse != null)
                    {
                        aggregateResponse = aggregateResponse.Merge(response);
                    }
                    else
                    {
                        aggregateResponse = response;
                    }
                }
            }

            return aggregateResponse;
        }

        private async Task<object> GetFirstNotEmptyResponseFromHandlers(ExportHandler<TRequest, TResponse>[] handlers, TRequest request)
        {
            var responses = new List<Task<TResponse>>();
            foreach (var handler in handlers)
            {
                responses.Add(handler.Handle(request));
            }

            foreach (object response in await Task.WhenAll(responses))
            {
                var canBeEmptyResponse = response as ICanBeEmptyResponse;
                if (canBeEmptyResponse != null)
                {
                    if (!canBeEmptyResponse.IsEmpty)
                    {
                        return response;
                    }
                }
                else if (response != null)
                {
                    return response;
                }
            }

            return null;
        }

        private async Task<object> HandleRequestForLanguage(string language, TRequest request, RequestPacket packet)
        {
            var exports = await _exports.Value;
            if (exports.TryGetValue(language, out var handlers))
            {
                if (_canBeAggregated)
                {
                    return await AggregateResponsesFromLanguageHandlers(handlers, request);
                }

                return await GetFirstNotEmptyResponseFromHandlers(handlers, request);
            }

            throw new NotSupportedException($"{language} does not support {EndpointName}");
        }

        private async Task<object> HandleAllRequest(TRequest request, RequestPacket packet)
        {
            if (!_canBeAggregated)
            {
                throw new NotSupportedException($"Must be able aggregate the response to spread them out across all plugins for {EndpointName}");
            }

            var exports = await _exports.Value;

            IAggregateResponse aggregateResponse = null;
            var responses = new List<Task<IAggregateResponse>>();
            foreach (var export in exports)
            {
                responses.Add(AggregateResponsesFromLanguageHandlers(export.Value, request));
            }

            foreach (IAggregateResponse exportResponse in await Task.WhenAll(responses))
            {
                if (aggregateResponse != null)
                {
                    aggregateResponse = aggregateResponse.Merge(exportResponse);
                }
                else
                {
                    aggregateResponse = exportResponse;
                }
            }

            object response = aggregateResponse;

            if (response != null)
            {
                return response;
            }

            return null;
        }

        private LanguageModel GetLanguageModel(JToken jtoken)
        {
            var response = new LanguageModel();
            var jobject = jtoken as JObject;
            if (jobject == null)
            {
                return response;
            }

            if (jobject.TryGetValue(nameof(LanguageModel.Language), StringComparison.OrdinalIgnoreCase, out var token))
            {
                response.Language = token.ToString();
            }


            if (jobject.TryGetValue(nameof(LanguageModel.FileName), StringComparison.OrdinalIgnoreCase, out token))
            {
                response.FileName = token.ToString();
            }

            return response;
        }

        private JToken DeserializeRequestObject(Stream readStream)
        {
            try
            {
                using (var streamReader = new StreamReader(readStream))
                {
                    using (var textReader = new JsonTextReader(streamReader))
                    {
                        return JToken.Load(textReader);
                    }
                }
            }
            catch
            {
                return new JObject();
            }
        }
    }
}
