namespace OmniSharp.Middleware.Endpoint.Exports
{
    class RequestHandlerExportHandler<TRequest, TResponse> : DelegateExportHandler<TRequest, TResponse>
    {
        public RequestHandlerExportHandler(string language, RequestHandler<TRequest, TResponse> handler)
         : base(language, handler.Handle)
        {
        }
    }
}
