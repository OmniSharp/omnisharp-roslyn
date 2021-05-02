using System.Threading.Tasks;
using OmniSharp.Mef;

namespace OmniSharp.Endpoint.Exports
{
    class RequestHandlerExportHandler<TRequest, TResponse> : ExportHandler<TRequest, TResponse>
    {
        private readonly IRequestHandler<TRequest, TResponse> _handler;

        public RequestHandlerExportHandler(string language, IRequestHandler<TRequest, TResponse> handler)
         : base(language)
        {
            _handler = handler;
        }

        public override int CompareTo(ExportHandler<TRequest, TResponse> other)
        {
            var otherHandler = other as RequestHandlerExportHandler<TRequest, TResponse>;
            if (otherHandler == null)
            {
                return 1;
            }

            return _handler.GetType().ToString().CompareTo(otherHandler._handler.GetType().ToString());
        }

        public override Task<TResponse> Handle(TRequest request)
        {
            return _handler.Handle(request);
        }
    }
}
