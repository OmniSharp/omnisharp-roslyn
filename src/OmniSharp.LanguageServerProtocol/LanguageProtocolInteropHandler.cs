using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Endpoint;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.UpdateBuffer;

namespace OmniSharp.LanguageServerProtocol
{
    public abstract class LanguageProtocolInteropHandler
    {
        public abstract Task<object> Handle(JToken request);

        public static LanguageProtocolInteropHandler Create<TRequest, TResponse>(
            IPredicateHandler languagePredicateHandler,
            OmniSharpEndpointMetadata metadata,
            IEnumerable<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> handlers,
            Lazy<LanguageProtocolInteropHandler<UpdateBufferRequest, object>> updateBufferHandler)
        {
            return new LanguageProtocolInteropHandler<TRequest, TResponse>(languagePredicateHandler, metadata, handlers.Where(x => x.Metadata.EndpointName == metadata.EndpointName), updateBufferHandler);
        }

        public static LanguageProtocolInteropHandler Factory(
            IPredicateHandler languagePredicateHandler,
            OmniSharpEndpointMetadata metadata,
            IEnumerable<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> handlers,
            Lazy<LanguageProtocolInteropHandler<UpdateBufferRequest, object>> updateBufferHandler)
        {
            var createMethod = typeof(LanguageProtocolInteropHandler).GetTypeInfo().DeclaredMethods.First(x => x.Name == nameof(LanguageProtocolInteropHandler.Create));
            return (LanguageProtocolInteropHandler)createMethod.MakeGenericMethod(metadata.RequestType, metadata.ResponseType).Invoke(null, new object[] { languagePredicateHandler, metadata, handlers, updateBufferHandler });
        }
    }

    public class LanguageProtocolInteropHandler<TRequest, TResponse> : LanguageProtocolInteropHandler
    {
        private readonly IPredicateHandler _languagePredicateHandler;
        private readonly Lazy<Dictionary<string, IRequestHandler<TRequest, TResponse>[]>> _exports;
        private readonly bool _hasLanguageProperty;
        private readonly bool _hasFileNameProperty;
        private readonly bool _canBeAggregated;
        private readonly Lazy<LanguageProtocolInteropHandler<UpdateBufferRequest, object>> _updateBufferHandler;

        public LanguageProtocolInteropHandler(IPredicateHandler languagePredicateHandler,
            OmniSharpEndpointMetadata metadata,
            IEnumerable<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> handlers,
            Lazy<LanguageProtocolInteropHandler<UpdateBufferRequest, object>> updateBufferHandler)
        {
            EndpointName = metadata.EndpointName;
            _languagePredicateHandler = languagePredicateHandler;

            _hasLanguageProperty = metadata.RequestType.GetRuntimeProperty(nameof(LanguageModel.Language)) != null;
            _hasFileNameProperty = metadata.RequestType.GetRuntimeProperty(nameof(Request.FileName)) != null;
            _canBeAggregated = typeof(IAggregateResponse).IsAssignableFrom(metadata.ResponseType);
            _updateBufferHandler = updateBufferHandler;

            _exports = new Lazy<Dictionary<string, IRequestHandler<TRequest, TResponse>[]>>(() =>LoadExportHandlers(handlers));
        }

        private Dictionary<string, IRequestHandler<TRequest, TResponse>[]> LoadExportHandlers(
            IEnumerable<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> handlers)
        {
            var interfaceHandlers = handlers
                .Select(export => (Language: export.Metadata.Language,
                    Handler: (IRequestHandler<TRequest, TResponse>) export.Value));

            // TODO: Support plugins? maybe never?
            // var plugins = _plugins.Where(x => x.Config.Endpoints.Contains(EndpointName))
            // .Select(plugin => (plugin.Config.Language, plugin));

            // Group handlers by language and sort each group for consistency
            return interfaceHandlers
                // .Concat(plugins)
                .GroupBy(export => export.Language, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(g => g.Handler).Select(z => z.Handler).ToArray());
        }

        public string EndpointName { get; }

        public override async Task<object> Handle(JToken requestObject)
        {
            var model = GetLanguageModel(requestObject);
            var request = requestObject.ToObject<TRequest>();
            if (request is Request && _updateBufferHandler.Value != null)
            {
                var realRequest = request as Request;
                if (!string.IsNullOrWhiteSpace(realRequest.FileName) &&
                    (realRequest.Buffer != null || realRequest.Changes != null))
                {
                    await _updateBufferHandler.Value.Handle(requestObject);
                }
            }

            if (_hasLanguageProperty)
            {
                // Handle cases where a request isn't aggregate and a language isn't specified.
                // This helps with editors calling a legacy end point, for example /metadata
                if (!_canBeAggregated && string.IsNullOrWhiteSpace(model.Language))
                {
                    model.Language = LanguageNames.CSharp;
                }

                return await HandleLanguageRequest(model.Language, request);
            }
            else if (_hasFileNameProperty)
            {
                var language = _languagePredicateHandler.GetLanguageForFilePath(model.FileName ?? string.Empty);
                return await HandleLanguageRequest(language, request);
            }
            else
            {
                var language = _languagePredicateHandler.GetLanguageForFilePath(string.Empty);
                if (!string.IsNullOrEmpty(language))
                {
                    return await HandleLanguageRequest(language, request);
                }
            }

            return await HandleAllRequest(request);
        }

        private Task<object> HandleLanguageRequest(string language, TRequest request)
        {
            if (!string.IsNullOrEmpty(language))
            {
                return HandleRequestForLanguage(language, request);
            }

            return HandleAllRequest(request);
        }

        private async Task<IAggregateResponse> AggregateResponsesFromLanguageHandlers(
            IRequestHandler<TRequest, TResponse>[] handlers, TRequest request)
        {
            if (!_canBeAggregated)
            {
                throw new NotSupportedException(
                    $"Must be able to aggregate responses from all handlers for {EndpointName}");
            }

            IAggregateResponse aggregateResponse = null;

            if (handlers.Length == 1)
            {
                var response = handlers[0].Handle(request);
                return (IAggregateResponse) await response;
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

        private async Task<object> GetFirstNotEmptyResponseFromHandlers(IRequestHandler<TRequest, TResponse>[] handlers,
            TRequest request)
        {
            var responses = new List<Task<TResponse>>();
            foreach (var handler in handlers)
            {
                responses.Add(handler.Handle(request));
            }

            foreach (object response in await Task.WhenAll(responses))
            {
                if (response is ICanBeEmptyResponse canBeEmptyResponse)
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

        private async Task<object> HandleRequestForLanguage(string language, TRequest request)
        {
            var exports = _exports.Value;
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

        private async Task<object> HandleAllRequest(TRequest request)
        {
            if (!_canBeAggregated)
            {
                throw new NotSupportedException(
                    $"Must be able to aggregate the response to spread them out across all plugins for {EndpointName}");
            }

            var exports = _exports.Value;

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
            if (!(jtoken is JObject jobject))
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
    }
}
