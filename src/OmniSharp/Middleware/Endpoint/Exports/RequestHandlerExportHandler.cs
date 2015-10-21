using System.Threading.Tasks;

namespace OmniSharp.Middleware.Endpoint.Exports
{
    class RequestHandlerExportHandler<TRequest, TResponse> : ExportHandler<TRequest, TResponse>
    {
        private readonly RequestHandler<TRequest, TResponse> _handler;

        public RequestHandlerExportHandler(string language, RequestHandler<TRequest, TResponse> handler)
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
