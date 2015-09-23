using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Microsoft.Framework.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Mef;
using OmniSharp.Middleware.Endpoint.Exports;
using OmniSharp.Models;
using OmniSharp.Plugins;

namespace OmniSharp.Middleware.Endpoint
{
    class LanguageModel
    {
        public string Language { get; set; }
        public string FileName { get; set; }
    }

    abstract class EndpointHandler
    {
        public abstract Task<object> Handle(HttpContext context);

        public static EndpointHandler Create<TRequest, TResponse>(IPredicateHandler languagePredicateHandler, CompositionHost host,
            ILogger logger, EndpointDescriptor item,
            Lazy<EndpointHandler<UpdateBufferRequest, object>> updateBufferHandler,
            IEnumerable<Plugin> plugins)
        {
            return new EndpointHandler<TRequest, TResponse>(languagePredicateHandler, host, logger, item, updateBufferHandler, plugins);
        }

        public static EndpointHandler Factory(IPredicateHandler languagePredicateHandler, CompositionHost host,
            ILogger logger, EndpointDescriptor item,
            Lazy<EndpointHandler<UpdateBufferRequest, object>> updateBufferHandler,
            IEnumerable<Plugin> plugins)
        {
            var createMethod = typeof(EndpointHandler).GetTypeInfo().DeclaredMethods.First(x => x.Name == nameof(EndpointHandler.Create));
            return (EndpointHandler)createMethod.MakeGenericMethod(item.RequestType, item.ResponseType).Invoke(null, new object[] { languagePredicateHandler, host, logger, item, updateBufferHandler, plugins });
        }
    }

    class EndpointHandler<TRequest, TResponse> : EndpointHandler
    {
        private readonly CompositionHost _host;
        private readonly IPredicateHandler _languagePredicateHandler;
        private readonly Lazy<Task<Dictionary<string, ExportHandler<TRequest, TResponse>>>> _exports;
        private readonly OmnisharpWorkspace _workspace;
        private readonly bool _hasLanguageProperty;
        private readonly bool _hasFileNameProperty;
        private readonly bool _isMergeable;
        private readonly ILogger _logger;
        private readonly IEnumerable<Plugin> _plugins;
        private readonly Lazy<EndpointHandler<UpdateBufferRequest, object>> _updateBufferHandler;

        public EndpointHandler(IPredicateHandler languagePredicateHandler, CompositionHost host, ILogger logger, EndpointDescriptor item, Lazy<EndpointHandler<UpdateBufferRequest, object>> updateBufferHandler, IEnumerable<Plugin> plugins)
        {
            EndpointName = item.EndpointName;
            _host = host;
            _logger = logger;
            _languagePredicateHandler = languagePredicateHandler;
            _plugins = plugins;
            _workspace = host.GetExport<OmnisharpWorkspace>();

            _hasLanguageProperty = item.RequestType.GetRuntimeProperty(nameof(LanguageModel.Language)) != null;
            _hasFileNameProperty = item.RequestType.GetRuntimeProperty(nameof(Request.FileName)) != null;
            _isMergeable = typeof(IMergeableResponse).IsAssignableFrom(item.ResponseType);
            _updateBufferHandler = updateBufferHandler;

            _exports = new Lazy<Task<Dictionary<string, ExportHandler<TRequest, TResponse>>>>(() => LoadExportHandlers());
        }

        private Task<Dictionary<string, ExportHandler<TRequest, TResponse>>> LoadExportHandlers()
        {
            var exports = _host.GetExports<Lazy<RequestHandler<TRequest, TResponse>, OmniSharpLanguage>>();
            var interfaceHandlers = exports
                .Select(export => new RequestHandlerExportHandler<TRequest, TResponse>(export.Metadata.Language, export.Value))
                .Cast<ExportHandler<TRequest, TResponse>>();

            var plugins = _plugins.Where(x => x.Config.Endpoints.Contains(EndpointName))
                .Select(plugin => new PluginExportHandler<TRequest, TResponse>(EndpointName, plugin))
                .Cast<ExportHandler<TRequest, TResponse>>();

            return Task.FromResult(interfaceHandlers
               .Concat(plugins)
               .ToDictionary(export => export.Language));
        }

        public string EndpointName { get; }

        public override Task<object> Handle(HttpContext context)
        {
            var requestObject = DeserializeRequestObject(context.Request.Body);
            var model = GetLanguageModel(requestObject);

            return Process(context, model, requestObject);
        }

        public async Task<object> Process(HttpContext context, LanguageModel model, JToken requestObject)
        {
            var request = requestObject.ToObject<TRequest>();
            if (request is Request && _updateBufferHandler.Value != null)
            {
                var realRequest = request as Request;
                if (!string.IsNullOrWhiteSpace(realRequest.FileName) && (realRequest.Buffer != null || realRequest.Changes != null))
                {
                    await _updateBufferHandler.Value.Process(context, model, requestObject);
                }
            }

            if (_hasLanguageProperty)
            {
                return await HandleLanguageRequest(model.Language, request, context);
            }
            else if (_hasFileNameProperty)
            {
                var language = _languagePredicateHandler.GetLanguageForFilePath(model.FileName ?? string.Empty);
                return await HandleLanguageRequest(language, request, context);
            }
            else
            {
                var language = _languagePredicateHandler.GetLanguageForFilePath(string.Empty);
                if (!string.IsNullOrEmpty(language))
                {
                    return await HandleLanguageRequest(language, request, context);
                }
            }

            return await HandleAllRequest(request, context);
        }

        private Task<object> HandleLanguageRequest(string language, TRequest request, HttpContext context)
        {
            if (!string.IsNullOrEmpty(language))
            {
                return HandleSingleRequest(language, request, context);
            }

            return HandleAllRequest(request, context);
        }

        private async Task<object> HandleSingleRequest(string language, TRequest request, HttpContext context)
        {
            var exports = await _exports.Value;
            ExportHandler<TRequest, TResponse> handler;
            if (exports.TryGetValue(language, out handler))
            {
                return await handler.Handle(request);
            }

            throw new NotSupportedException($"{language} does not support {EndpointName}");
        }

        private async Task<object> HandleAllRequest(TRequest request, HttpContext context)
        {
            if (!_isMergeable)
            {
                throw new NotSupportedException($"Responses must be mergable to spread them out across all plugins for {EndpointName}");
            }

            var exports = await _exports.Value;

            IMergeableResponse mergableResponse = null;

            var responses = new List<Task<TResponse>>();
            foreach (var handler in exports.Values)
            {
                responses.Add(handler.Handle(request));
            }

            foreach (IMergeableResponse exportResponse in await Task.WhenAll(responses))
            {
                if (mergableResponse != null)
                {
                    mergableResponse = mergableResponse.Merge(exportResponse);
                }
                else
                {
                    mergableResponse = exportResponse;
                }
            }

            object response = mergableResponse;

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

            JToken token;
            if (jobject.TryGetValue(nameof(LanguageModel.Language), StringComparison.OrdinalIgnoreCase, out token))
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
            if (readStream.Length > 0)
            {
                return JToken.Load(new JsonTextReader(new StreamReader(readStream)));
            }
            return new JObject();
        }
    }
}
