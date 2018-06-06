using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Cake.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Cake.Services.RequestHandlers
{
    public abstract class CakeRequestHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    {
        private string _endpointName;
        public string EndpointName
        {
            get
            {
                if(string.IsNullOrEmpty(_endpointName))
                {
                    _endpointName = GetType().GetTypeInfo().GetCustomAttribute<OmniSharpHandlerAttribute>()?.EndpointName;
                }

                return _endpointName;
            }
        }

        [ImportMany]
        public IEnumerable<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> Handlers { get; set; }
        public OmniSharpWorkspace Workspace { get; }
        public Lazy<IRequestHandler<TRequest, TResponse>> Service { get; }

        protected CakeRequestHandler(OmniSharpWorkspace workspace)
        {
            Workspace = workspace;
            Service = new Lazy<IRequestHandler<TRequest, TResponse>>(() =>
            {
                return Handlers.FirstOrDefault(s =>
                    s.Metadata.EndpointName.Equals(EndpointName, StringComparison.Ordinal) &&
                    s.Metadata.Language.Equals(LanguageNames.CSharp, StringComparison.Ordinal))?.Value 
                    as IRequestHandler<TRequest, TResponse>;
            });
        }

        public virtual async Task<TResponse> Handle(TRequest request)
        {
            var service = Service.Value;
            if(service == null)
            {
                throw new NotSupportedException();
            }

            request = await TranslateRequestAsync(request);

            var response = IsValid(request)
                ? await service.Handle(request)
                : default(TResponse);

            return await TranslateResponse(response, request);
        }

        protected virtual bool IsValid(TRequest request) => true;

        protected virtual async Task<TRequest> TranslateRequestAsync(TRequest req)
        {
            var request = req as Request;

            if (request != null)
            {
                await request.TranslateAsync(Workspace);
            }

            return req;
        }

        protected virtual Task<TResponse> TranslateResponse(TResponse response, TRequest request)
        {
            return Task.FromResult(response);
        }
    }
}
