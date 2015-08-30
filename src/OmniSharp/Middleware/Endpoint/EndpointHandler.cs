using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Newtonsoft.Json;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Middleware.Endpoint
{
    class EndpointHandler
    {
        private static readonly MethodInfo GetExportsMethod = typeof(EndpointHandler)
            .GetTypeInfo()
            .DeclaredMethods
            .Single(methodInfo => methodInfo.Name == nameof(GetExports));
        private static Type TaskType = typeof(Task<>);
        private static Type WorkspaceFuncType = typeof(Func<,,>);
        private static Type FuncType = typeof(Func<,>);

        private readonly CompositionHost _host;
        private readonly Type _workspaceDelegateType;
        private readonly Type _delegateType;
        private readonly Type _interfaceType;
        private readonly LanguagePredicateHandler _languagePredicateHandler;
        private readonly IDictionary<string, ExportHandler> _exports;
        private readonly Type _requestType;
        private readonly Type _responseType;
        private readonly OmnisharpWorkspace _workspace;

        public EndpointHandler(OmnisharpWorkspace workspace, LanguagePredicateHandler languagePredicateHandler, CompositionHost host, OmniSharp.Endpoints.EndpointMapItem item)
        {
            EndpointName = item.EndpointName;
            _host = host;
            _languagePredicateHandler = languagePredicateHandler;
            _interfaceType = item.InterfaceType;
            _workspaceDelegateType = WorkspaceFuncType.MakeGenericType(typeof(OmnisharpWorkspace), item.RequestType, TaskType.MakeGenericType(item.ResponseType));
            _delegateType = FuncType.MakeGenericType(item.RequestType, TaskType.MakeGenericType(item.ResponseType));
            _requestType = item.RequestType;
            _responseType = item.ResponseType;
            _workspace = workspace;

            var workspaceDelegateExports = (IEnumerable<ExportHandler>)GetExportsMethod.MakeGenericMethod(_workspaceDelegateType).Invoke(this, new object[] { });
            var delegateExports = (IEnumerable<ExportHandler>)GetExportsMethod.MakeGenericMethod(_delegateType).Invoke(this, new object[] { });
            var interfaceExports = (IEnumerable<ExportHandler>)GetExportsMethod.MakeGenericMethod(_interfaceType).Invoke(this, new object[] { });
            _exports = delegateExports
                .Concat(workspaceDelegateExports)
                .Concat(interfaceExports)
                .ToDictionary(export => export.Language);
        }

        public string EndpointName { get; }

        public async Task Handle(HttpContext context)
        {
            var request = await DeserializeRequestObject(context.Request.Body);
            var language = await _languagePredicateHandler.GetLanguageForFilePath(request.FileName);

            ExportHandler handler;
            if (_exports.TryGetValue(language, out handler))
            {
                var response = await handler.Handle(_workspace, request);
                SerializeResponseObject(context.Response, response);
            }
        }

        private Task<Request> DeserializeRequestObject(Stream readStream)
        {
            using (var jsonReader = new JsonTextReader(new StreamReader(readStream)))
            {
                jsonReader.CloseInput = false;

                var jsonSerializer = JsonSerializer.Create(/*TODO: SerializerSettings*/);

                try
                {
                    return Task.FromResult((Request)jsonSerializer.Deserialize(jsonReader, _requestType));
                }
                finally
                {
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

        private IEnumerable<ExportHandler> GetExports<T>()
        {
            var typeInfo = typeof(T).GetTypeInfo();
            var exports = _host.GetExports<Lazy<T, OmniSharpLanguage>>();
            foreach (var export in exports)
            {
                if (typeInfo.IsInterface)
                {
                    yield return new InterfaceExportHandler(export.Metadata.Language, typeof(T), _responseType, export.Value);
                }
                else if (typeInfo.GenericTypeArguments.Length == 2)
                {
                    yield return CreateDelegateExportHandler(export.Metadata.Language, typeof(T).GetTypeInfo(), export.Value);
                }
                else if (typeInfo.GenericTypeArguments.Length == 3)
                {
                    yield return CreateDelegateExportHandler(export.Metadata.Language, typeof(T).GetTypeInfo(), export.Value);
                }
            }
        }

        abstract class ExportHandler
        {
            protected ExportHandler(string language)
            {
                Language = language;
            }

            public string Language { get; }
            public abstract Task<object> Handle(OmnisharpWorkspace workspace, object request);
        }

        private ExportHandler CreateDelegateExportHandler(string language, TypeInfo typeInfo, object value)
        {
            var genericType = typeof(DelegateExportHandler<,>).MakeGenericType(_requestType, _responseType);
            return (ExportHandler)Activator.CreateInstance(genericType, language, value);
        }

        private ExportHandler CreateWorkspaceDelegateExportHandler(string language, TypeInfo typeInfo, object value)
        {
            var genericType = typeof(WorkspaceDelegateExportHandler<,>).MakeGenericType(_requestType, _responseType);
            return (ExportHandler)Activator.CreateInstance(genericType, language, value);
        }

        class DelegateExportHandler<TRequest, TResponse> : ExportHandler
        {
            private readonly Func<TRequest, Task<TResponse>> _handler;

            public DelegateExportHandler(string language, Func<TRequest, Task<TResponse>> handler) : base(language)
            {
                _handler = handler;
            }

            public async override Task<object> Handle(OmnisharpWorkspace workspace, object request)
            {
                return await _handler((TRequest)request);
            }
        }

        class WorkspaceDelegateExportHandler<TRequest, TResponse> : ExportHandler
        {
            private readonly Func<OmnisharpWorkspace, TRequest, Task<TResponse>> _handler;

            public WorkspaceDelegateExportHandler(string language, Func<OmnisharpWorkspace, TRequest, Task<TResponse>> handler) : base(language)
            {
                _handler = handler;
            }

            public async override Task<object> Handle(OmnisharpWorkspace workspace, object request)
            {
                return await _handler(workspace, (TRequest)request);
            }
        }

        class InterfaceExportHandler : ExportHandler
        {
            private readonly object _instance;
            private readonly Lazy<MethodInfo> _methodInfo;
            private readonly Lazy<MethodInfo> _methodInvoke;

            public InterfaceExportHandler(string language, Type interfaceType, Type responseType, object instance) : base(language)
            {
                _instance = instance;
                _methodInfo = new Lazy<MethodInfo>(() => interfaceType.GetTypeInfo().DeclaredMethods.Single());
                _methodInvoke = new Lazy<MethodInfo>(() => typeof(InterfaceExportHandler).GetTypeInfo().DeclaredMethods.Single(x => x.Name == nameof(MethodInvoke)).MakeGenericMethod(responseType));
            }

            public async override Task<object> Handle(OmnisharpWorkspace workspace, object request)
            {
                return await ((Task<object>)_methodInvoke.Value.Invoke(this, new object[] { request }));
            }

            private async Task<object> MethodInvoke<T>(object request)
            {
                return await ((Task<T>)_methodInfo.Value.Invoke(_instance, new object[] { request }));
            }
        }
    }
}
