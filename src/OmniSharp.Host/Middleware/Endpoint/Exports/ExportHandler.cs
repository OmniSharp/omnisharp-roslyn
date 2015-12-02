using System.Threading.Tasks;

namespace OmniSharp.Middleware.Endpoint.Exports
{
    abstract class ExportHandler<TRequest, TResponse>
    {
        protected ExportHandler(string language)
        {
            Language = language;
        }

        public string Language { get; }
        public abstract Task<TResponse> Handle(TRequest request);
    }
}
