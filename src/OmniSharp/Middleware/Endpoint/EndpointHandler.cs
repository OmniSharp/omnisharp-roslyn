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
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Plugins;

namespace OmniSharp.Middleware.Endpoint
{
    class LanguageModel
    {
        public string Language { get; set; }
        public string FileName { get; set; }
    }

    class EndpointHandler
    {
        private static readonly MethodInfo GetDelegateExportsMethod = typeof(EndpointHandler)
            .GetTypeInfo()
            .DeclaredMethods
            .Single(methodInfo => methodInfo.Name == nameof(GetDelegateExports));

        private static readonly MethodInfo GetRequestHandlerExportsMethod = typeof(EndpointHandler)
            .GetTypeInfo()
            .DeclaredMethods
            .Single(methodInfo => methodInfo.Name == nameof(GetRequestHandlerExports));

        private static Type TaskType = typeof(Task<>);
        private static Type FuncType = typeof(Func<,>);
        private static Type RequestHandlerType = typeof(RequestHandler<,>);

        private readonly CompositionHost _host;
        private readonly Type _delegateType;
        private readonly Type _requestHandlerType;
        private readonly LanguagePredicateHandler _languagePredicateHandler;
        private readonly Lazy<Task<Dictionary<string, ExportHandler>>> _exports;
        private readonly Type _requestType;
        private readonly Type _responseType;
        private readonly OmnisharpWorkspace _workspace;
        private readonly bool _hasLanguageProperty;
        private readonly bool _hasFileNameProperty;
        private readonly bool _isMergeable;
        private readonly ILogger _logger;
        private readonly IEnumerable<OutOfProcessPlugin> _plugins;

        public EndpointHandler(OmnisharpWorkspace workspace, LanguagePredicateHandler languagePredicateHandler, CompositionHost host, ILogger logger, OmniSharp.Endpoints.EndpointMapItem item, IEnumerable<OutOfProcessPlugin> plugins)
        {
            EndpointName = item.EndpointName;
            _host = host;
            _logger = logger;
            _workspace = workspace;
            _languagePredicateHandler = languagePredicateHandler;
            _plugins = plugins;

            _delegateType = FuncType.MakeGenericType(item.RequestType, TaskType.MakeGenericType(item.ResponseType));
            _requestHandlerType = RequestHandlerType.MakeGenericType(item.RequestType, item.ResponseType);
            _requestType = item.RequestType;
            _responseType = item.ResponseType;

            _hasLanguageProperty = item.RequestType.GetRuntimeProperty(nameof(LanguageModel.Language)) != null;
            _hasFileNameProperty = item.RequestType.GetRuntimeProperty(nameof(Request.FileName)) != null;
            _isMergeable = typeof(IMergeableResponse).IsAssignableFrom(item.ResponseType);

            _exports = new Lazy<Task<Dictionary<string, ExportHandler>>>(() => LoadExportHandlers());
        }

        private Task<Dictionary<string, ExportHandler>> LoadExportHandlers()
        {
            var delegateExports = (IEnumerable<ExportHandler>)GetDelegateExportsMethod.MakeGenericMethod(_delegateType).Invoke(this, new object[] { });
            var interfaceExports = (IEnumerable<ExportHandler>)GetRequestHandlerExportsMethod.MakeGenericMethod(_requestHandlerType).Invoke(this, new object[] { });

            var plugins = _plugins.Where(x => x.Config.Endpoints.Contains(EndpointName))
                .Select(plugin => new PluginExportHandler(EndpointName, plugin, _responseType));

            return Task.FromResult(delegateExports
               .Concat(interfaceExports)
               .Concat(plugins)
               .ToDictionary(export => export.Language));
        }

        public string EndpointName { get; }

        public Task Handle(HttpContext context)
        {
            var model = GetLanguageModel(context.Request.Body);
            if (_hasLanguageProperty)
            {
                return HandleLanguageRequest(model.Language, context);
            }

            if (_hasFileNameProperty)
            {
                var language = _languagePredicateHandler.GetLanguageForFilePath(model.FileName);
                if (string.IsNullOrEmpty(language))
                {
                    throw new NotSupportedException($"Could not determine language for {model.FileName} (does is it not support {EndpointName}?)");
                }
                return HandleLanguageRequest(language, context);
            }

            return HandleAllRequest(context);
        }

        private Task HandleLanguageRequest(string language, HttpContext context)
        {
            if (!string.IsNullOrEmpty(language))
            {
                return HandleSingleRequest(language, context);
            }

            return HandleAllRequest(context);
        }

        private async Task HandleSingleRequest(string language, HttpContext context)
        {
            var request = DeserializeRequestObject(context.Request.Body);
            var exports = await _exports.Value;
            ExportHandler handler;
            if (exports.TryGetValue(language, out handler))
            {
                var response = await handler.Handle(request);
                SerializeResponseObject(context.Response, response);
                return;
            }

            throw new NotSupportedException($"{language} does not support {EndpointName}");
        }

        private async Task HandleAllRequest(HttpContext context)
        {
            if (!_isMergeable)
            {
                throw new NotSupportedException($"Responses must be mergable to spread them out across all plugins for {EndpointName}");
            }

            var exports = await _exports.Value;
            var request = DeserializeRequestObject(context.Request.Body);

            IMergeableResponse response = null;
            var responses = new List<Task<object>>();

            foreach (var handler in exports.Values)
            {
                responses.Add(handler.Handle(request));
            }

            foreach (IMergeableResponse exportResponse in await Task.WhenAll(responses))
            {
                if (response != null)
                {
                    response = response.Merge(exportResponse);
                }
                else
                {
                    response = exportResponse;
                }
            }

            SerializeResponseObject(context.Response, response);
        }

        private LanguageModel GetLanguageModel(Stream readStream)
        {
            using (var jsonReader = new JsonTextReader(new StreamReader(readStream)))
            {
                jsonReader.CloseInput = false;

                var jsonSerializer = JsonSerializer.Create(/*TODO: SerializerSettings*/);

                var response = new LanguageModel();
                try
                {
                    var result = jsonSerializer.Deserialize<Dictionary<string, object>>(jsonReader);
                    if (result.ContainsKey(nameof(LanguageModel.Language)))
                    {
                        response.Language = (string)result[nameof(LanguageModel.Language)];
                    }

                    if (result.ContainsKey(nameof(LanguageModel.FileName)))
                    {
                        response.FileName = (string)result[nameof(LanguageModel.FileName)];
                    }
                }
                finally { }

                return response;
            }
        }

        private object DeserializeRequestObject(Stream readStream)
        {
            using (var jsonReader = new JsonTextReader(new StreamReader(readStream)))
            {
                jsonReader.CloseInput = false;

                var jsonSerializer = JsonSerializer.Create(/*TODO: SerializerSettings*/);
                try
                {
                    return jsonSerializer.Deserialize(jsonReader, _requestType);
                }
                catch
                {
                    return null;
                }
            }
        }

        private void SerializeResponseObject(HttpResponse response, object value)
        {
            using (var writer = new StreamWriter(response.Body))
            {
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    jsonWriter.CloseOutput = false;
                    var jsonSerializer = JsonSerializer.Create(/*TODO: SerializerSettings*/);
                    jsonSerializer.Serialize(jsonWriter, value);
                }
            }
        }

        private IEnumerable<ExportHandler> GetRequestHandlerExports<T>()
        {
            var typeInfo = typeof(T).GetTypeInfo();
            var exports = _host.GetExports<Lazy<T, OmniSharpLanguage>>();
            foreach (var export in exports)
            {
                var genericType = typeof(RequestHandlerExportHandler<,>).MakeGenericType(_requestType, _responseType);
                yield return (ExportHandler)Activator.CreateInstance(genericType, export.Metadata.Language, export.Value);
            }
        }

        private IEnumerable<ExportHandler> GetDelegateExports<T>()
        {
            var typeInfo = typeof(T).GetTypeInfo();
            var exports = _host.GetExports<Lazy<T, OmniSharpLanguage>>();
            foreach (var export in exports)
            {
                var genericType = typeof(DelegateExportHandler<,>).MakeGenericType(_requestType, _responseType);
                yield return (ExportHandler)Activator.CreateInstance(genericType, export.Metadata.Language, export.Value);
            }
        }

        abstract class ExportHandler
        {
            protected ExportHandler(string language)
            {
                Language = language;
            }

            public string Language { get; }
            public abstract Task<object> Handle(object request);
        }

        class DelegateExportHandler<TRequest, TResponse> : ExportHandler
        {
            private readonly Func<TRequest, Task<TResponse>> _handler;
            public DelegateExportHandler(string language, Func<TRequest, Task<TResponse>> handler)
             : base(language)
            {
                _handler = handler;
            }

            public async override Task<object> Handle(object request)
            {
                return await _handler((TRequest)request);
            }
        }

        class RequestHandlerExportHandler<TRequest, TResponse> : DelegateExportHandler<TRequest, TResponse>
        {
            public RequestHandlerExportHandler(string language, RequestHandler<TRequest, TResponse> handler)
             : base(language, handler.Handle)
            { }
        }

        class PluginExportHandler : ExportHandler
        {
            private readonly string _endpoint;
            private readonly OutOfProcessPlugin _plugin;
            private readonly Type _responseType;

            public PluginExportHandler(string endpoint, OutOfProcessPlugin plugin, Type responseType) : base(plugin.Config.Language)
            {
                _endpoint = endpoint;
                _plugin = plugin;
                _responseType = responseType;
            }

            public override Task<object> Handle(object request)
            {
                return _plugin.Handle(_endpoint, request, _responseType);
            }
        }
    }
}
