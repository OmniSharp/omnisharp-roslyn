using System.Threading.Tasks;
using OmniSharp.Mef;

namespace OmniSharp.Middleware.Endpoint.Exports
{
    class RequestHandlerExportHandler<TRequest, TResponse> : ExportHandler<TRequest, TResponse>
    {
        private readonly IRequestHandler<TRequest, TResponse> _handler;

        public RequestHandlerExportHandler(string language, IRequestHandler<TRequest, TResponse> handler)
         : base(language)
        {
            _handler = handler;
        }

        public override Task<TResponse> Handle(TRequest request)
        {
            return _handler.Handle(request);
        }
    }
}
