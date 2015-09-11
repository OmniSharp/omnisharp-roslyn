using System;
using System.Threading.Tasks;

namespace OmniSharp.Middleware.Endpoint.Exports
{
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
}
