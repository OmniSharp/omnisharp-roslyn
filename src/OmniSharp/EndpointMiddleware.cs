using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using Microsoft.Framework.Logging;
using Newtonsoft.Json;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp
{
    // You may need to install the Microsoft.AspNet.Http.Abstractions package into your project
    public class EndpointMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HashSet<string> _endpoints;
        private readonly IReadOnlyDictionary<string, EndpointHandler> _endpointHandlers;
        private readonly LanguagePredicateHandler _languagePredicateHandler;
        private readonly OmnisharpWorkspace _workspace;
        //private readonly CompositionHost _host;
        private readonly ILogger _logger;

        public EndpointMiddleware(RequestDelegate next, OmnisharpWorkspace workspace, ILoggerFactory loggerFactory)
        {
            _next = next;
            _workspace = workspace;
            //_host = workspace.PluginHost;
            _logger = loggerFactory.CreateLogger<EndpointMiddleware>();
            _languagePredicateHandler = new LanguagePredicateHandler(/*_host.GetExports<Lazy<Func<string, Task<bool>>, OmniSharpLanguage>>(), */null, _logger);

            var endpointDelegates = typeof(OmniSharp.EndpointDelegates)
                .GetTypeInfo()
                .DeclaredNestedTypes
                .Where(typeInfo => typeInfo.GetCustomAttribute<EndpointNameAttribute>() != null)
                .Select(typeInfo =>
                {
                    var instance = (OmniSharpEndpointAttribute)Activator.CreateInstance(typeInfo.AsType(), string.Empty);
                    return new
                    {
                        EndpointName = typeInfo.GetCustomAttribute<EndpointNameAttribute>().Name,
                        Type = instance.ContractType
                    };
                })
                .ToArray();

            var endpointInterfaces = typeof(OmniSharp.Endpoints)
                .GetTypeInfo()
                .DeclaredNestedTypes
                .Where(typeInfo => typeInfo.GetCustomAttribute<EndpointNameAttribute>() != null)
                .Select(typeInfo =>
                {
                    var instance = (OmniSharpEndpointAttribute)Activator.CreateInstance(typeInfo.AsType(), string.Empty);
                    return new
                    {
                        EndpointName = typeInfo.GetCustomAttribute<EndpointNameAttribute>().Name,
                        Type = instance.ContractType
                    };
                })
                .ToArray();

            var endpoints = endpointDelegates.Join(endpointInterfaces, dele => dele.EndpointName, inter => inter.EndpointName,
            (dele, inter) => new EndpointHandler(_languagePredicateHandler, /*_host,*/null, dele.EndpointName, dele.Type, inter.Type, _logger));

            _endpoints = new HashSet<string>(endpointDelegates.Select(x => x.EndpointName).Distinct());

            var endpointHandlers = endpoints.ToDictionary(x => x.EndpointName, x => x);
            _endpointHandlers = new ReadOnlyDictionary<string, EndpointHandler>(endpointHandlers);
        }

        public Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path.HasValue)
            {
                var endpoint = httpContext.Request.Path.Value;
                if (_endpoints.Contains(endpoint))
                {
                    OmnisharpWorkspace.Instance.ConfigurePluginHost(null);
                    _logger.LogInformation($"Endpoint Request: {httpContext.Request.Path}");
                    EndpointHandler handler;
                    if (_endpointHandlers.TryGetValue(endpoint, out handler))
                    {
                        return handler.Handle(httpContext);
                    }
                }
            }

            return _next(httpContext);
        }
    }

    class LanguagePredicateHandler
    {
        private readonly IDictionary<string, Lazy<Func<string, Task<bool>>>> _exports;
        private readonly ILogger _logger;

        public LanguagePredicateHandler(IDictionary<string, Lazy<Func<string, Task<bool>>>> exports, ILogger logger)
        {
            _exports = exports;
            _logger = logger;
        }

        public async Task<string> GetLanguageForFilePath(string filePath)
        {
            var exports = _exports ?? OmnisharpWorkspace.Instance.GetExportsByLanguage<Func<string, Task<bool>>>();
            _logger.LogInformation($"Language selectors: {exports.Count()}");
            foreach (var export in exports)
            {
                if (await export.Value.Value(filePath))
                {
                    return export.Key;
                }
            }

            return null;
        }
    }

    class EndpointHandler
    {
        private static readonly MethodInfo GetExportsMethod = typeof(EndpointHandler)
            .GetTypeInfo()
            .DeclaredMethods
            .Single(methodInfo => methodInfo.Name == nameof(GetExports));
        private readonly CompositionHost _host;
        private readonly Type _delegateType;
        private readonly Type _interfaceType;
        private readonly LanguagePredicateHandler _languagePredicateHandler;
        private /*readonly*/ IDictionary<string, ExportHandler> _exports;
        private readonly Type _requestType;
        private readonly ILogger _logger;

        public EndpointHandler(LanguagePredicateHandler languagePredicateHandler, CompositionHost host, string endpointName, Type delegateType, Type interfaceType, ILogger logger)
        {
            EndpointName = endpointName;
            _host = host;
            _delegateType = delegateType;
            _interfaceType = interfaceType;
            _languagePredicateHandler = languagePredicateHandler;
            _requestType = interfaceType.GetTypeInfo().DeclaredMethods.Single().GetParameters()[0].ParameterType;
            _logger = logger;

            /*var delegateExports = (IEnumerable<ExportHandler>)GetExportsMethod.MakeGenericMethod(_delegateType).Invoke(this, new object[] { });
            var interfaceExports = (IEnumerable<ExportHandler>)GetExportsMethod.MakeGenericMethod(_interfaceType).Invoke(this, new object[] { });
            _exports = delegateExports
                .Concat(interfaceExports)
                .ToDictionary(export => export.Language);*/
        }

        public string EndpointName { get; }

        public async Task Handle(HttpContext context)
        {
            var delegateExports = (IEnumerable<ExportHandler>)GetExportsMethod.MakeGenericMethod(_delegateType).Invoke(this, new object[] { });
            var interfaceExports = (IEnumerable<ExportHandler>)GetExportsMethod.MakeGenericMethod(_interfaceType).Invoke(this, new object[] { });
            _exports = delegateExports
                .Concat(interfaceExports)
                .ToDictionary(export => export.Language);

            var request = await DeserializeRequestObject(context.Request.Body);
            _logger.LogInformation(request.FileName);

            var language = await _languagePredicateHandler.GetLanguageForFilePath(request.FileName);
            _logger.LogInformation(language);

            ExportHandler handler;
            if (_exports.TryGetValue(language, out handler))
            {
                var response = await handler.Handle(request);
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
            var exports = /*_host*/OmnisharpWorkspace.Instance.PluginHost.GetExports<Lazy<T, OmniSharpLanguage>>();
            foreach (var export in exports)
            {
                if (typeInfo.IsInterface)
                {
                    yield return new InterfaceExportHandler(export.Metadata.Language, typeof(T), export.Value);
                }
                else
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
            public abstract Task<object> Handle(object request);

        }

        private ExportHandler CreateDelegateExportHandler(string language, TypeInfo typeInfo, object value)
        {
            var requestType = typeInfo.GenericTypeParameters[0];
            var responseType = typeInfo.GenericTypeParameters[1].GetGenericArguments()[0];

            var genericType = typeof(DelegateExportHandler<,>).MakeGenericType(requestType, responseType);
            return (ExportHandler)Activator.CreateInstance(genericType, language, value);
        }

        class DelegateExportHandler<TRequest, TResponse> : ExportHandler
        {
            private readonly Func<TRequest, Task<TResponse>> _handler;

            public DelegateExportHandler(string language, Func<TRequest, Task<TResponse>> handler) : base(language)
            {
                _handler = handler;
            }

            public async override Task<object> Handle(object request)
            {
                return await _handler((TRequest)request);
            }
        }

        class InterfaceExportHandler : ExportHandler
        {
            private readonly object _instance;
            private readonly MethodInfo _methodInfo;

            public InterfaceExportHandler(string language, Type interfaceType, object instance) : base(language)
            {
                _instance = instance;
                _methodInfo = interfaceType.GetTypeInfo().DeclaredMethods.Single();
            }

            public override Task<object> Handle(object request)
            {
                return ((Task<object>)_methodInfo.Invoke(_instance, new object[] { request }));
            }
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class EndpointMiddlewareExtensions
    {
        public static IApplicationBuilder UseEndpointMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<EndpointMiddleware>();
        }
    }
}
