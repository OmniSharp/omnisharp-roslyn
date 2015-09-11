using System.Threading.Tasks;

namespace OmniSharp.Middleware.Endpoint.Exports
{
    abstract class ExportHandler
    {
        protected ExportHandler(string language)
        {
            Language = language;
        }

        public string Language { get; }
        public abstract Task<object> Handle(object request);
    }
}
